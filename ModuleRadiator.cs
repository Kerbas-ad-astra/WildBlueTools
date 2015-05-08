using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;

/*
Source code copyright 2015, by Michael Billard (Angel-125)
License: CC BY-NC-SA 4.0
License URL: https://creativecommons.org/licenses/by-nc-sa/4.0/
If you want to use this code, give me a shout on the KSP forums! :)
Wild Blue Industries is trademarked by Michael Billard and may be used for non-commercial purposes. All other rights reserved.
Note that Wild Blue Industries is a ficticious entity created for entertainment purposes. It is in no way meant to represent a real entity.
Any similarity to a real entity is purely coincidental.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
namespace WildBlueIndustries
{
    public enum CoolingCycleModes
    {
        active,  //Heat is pulled into the radiator and relies on stock game to cool off. Used when part does not deploy radiators.
        passive, //Heat is not pulled into the radiator and it relies upon stock game heat management
        closed,  //Heat is actively transferred into the radiator, but it relies on stock game to cool off.
        open     //Heat is actively transferred into the radiator, and coolant is expelled to rapidly cool the radiator.
    }

    public struct CoolantResource
    {
        public string name;
        public ResourceFlowMode flowMode;
        public float ratio;
    }

    public delegate void RadiatorDestroyed(ModuleRadiator doomed);

    public class ModuleRadiator : ModuleDeployableSolarPanel
    {
        //768k is when object should start glowing red, but we lower it to make the animation glow look right.
        private const float kGlowTempOffset = 600f; //My custom Draper Point...
        private const string kDefaultCoolant = "Coolant";

        //Status text
        [KSPField(guiActive = true, guiName = "Temperature")]
        public string radiatorTemperature;

        //A value between 0 and 1, used to determine how much of the radiator's max
        //temperature may be dedicated to heat management.
        [KSPField(isPersistant = true)]
        public float workingTempFactor;

        //How many units of coolant to dump overboard during
        //open-cycle cooling.
        [KSPField(isPersistant = true)]
        public float coolantDumpRate;

        //How many units of coolant/sec is lost while the ship is
        //under acceleration
        [KSPField(isPersistant = true)]
        public float lossRateAccelerating;

        //Current cooling cycle mode.
        [KSPField(isPersistant = true)]
        public CoolingCycleModes coolingCycleMode;

        //Amount of ec per second required to run the radiator, if any.
        [KSPField(isPersistant = true)]
        public double ecRequired;

        public RadiatorDestroyed radiatorDestroyedDelegate;

        protected List<CoolantResource> coolantResources = new List<CoolantResource>();
        protected double maxThermalTransfer = 0;
        protected double currentThermalTransfer = 0;
        PartResourceDefinitionList definitions = PartResourceLibrary.Instance.resourceDefinitions;
        PartResourceDefinition electricChargeDef;

        #region Overrides and API
        [KSPAction("Toggle Cooling Cycle")]
        public void ToggleGoolingModeAction(KSPActionParam param)
        {
            ToggleCoolingMode();
        }

        [KSPAction("Open Cooling Cycle")]
        public void OpenModeAction(KSPActionParam param)
        {
            coolingCycleMode = CoolingCycleModes.open;
            Events["ToggleCoolingMode"].guiName = "Cooling Mode (open)";
        }

        [KSPAction("Closed Cooling Cycle")]
        public void ClosedModeAction(KSPActionParam param)
        {
            coolingCycleMode = CoolingCycleModes.closed;
            Events["ToggleCoolingMode"].guiName = "Cooling Mode (closed)";
        }

        [KSPEvent(guiActive = true, guiName = "Cooling Mode", active = true, externalToEVAOnly = false, unfocusedRange = 3.0f, guiActiveUnfocused = true)]
        public void ToggleCoolingMode()
        {
            if (coolingCycleMode == CoolingCycleModes.closed)
            {
                coolingCycleMode = CoolingCycleModes.open;
                Events["ToggleCoolingMode"].guiName = "Cooling Mode (open)";
            }

            else
            {
                coolingCycleMode = CoolingCycleModes.closed;
                Events["ToggleCoolingMode"].guiName = "Cooling Mode (closed)";
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            //Get the resource definition for electric charge.
            electricChargeDef = definitions["ElectricCharge"];

            //Since we are based upon the ModuleDeployableSolarPanel, hide its gui
            Fields["sunAOA"].guiActive = false;
            Fields["flowRate"].guiActive = false;

            //Set cooling mode. For now, default is closed.
            coolingCycleMode = CoolingCycleModes.closed;
            Events["ToggleCoolingMode"].guiName = "Cooling Mode (closed)";

            //Dig into the proto part and find the coolant resource nodes.
            getCoolantNodes();

            this.part.OnJustAboutToBeDestroyed = OnAboutToBeDestroyed;
        }

        public void OnAboutToBeDestroyed()
        {
            if (radiatorDestroyedDelegate != null)
                radiatorDestroyedDelegate(this);
        }

        public void UpdateState()
        {
            //Do we have enough electricity to run the radiator?
            if (ecRequired > 0.001 && panelState == panelStates.EXTENDED)
            {
                double ecPerTimeTick = ecRequired * TimeWarp.fixedDeltaTime;
                double ecSupplied = this.part.vessel.rootPart.RequestResource(electricChargeDef.id, ecPerTimeTick, ResourceFlowMode.ALL_VESSEL);

                if (ecSupplied < ecPerTimeTick)
                    return;
            }

            //For open-cycle cooling, dump coolant resources overboard and adjust thermal energy accordingly.
            if (coolingCycleMode == CoolingCycleModes.open || (lossRateAccelerating > 0f && this.part.vessel.acceleration.magnitude > 0f) )
            {
                //Now go through the list of coolants and dump them overboard, carrying heat with them.
                foreach (CoolantResource coolant in coolantResources)
                {
                    if (coolingCycleMode == CoolingCycleModes.open)
                        dumpCoolant(coolant, coolantDumpRate);

                    if (lossRateAccelerating > 0f)
                        dumpCoolant(coolant, lossRateAccelerating);
                }
            }

            //Now set the radiator color
            //The game's built-in shader is working but it doesn't look as nice.
            SetRadiatorColor();

            //If we have heat to transfer in, then do so and reset
            //We do this in one chunk for game performance.
            if (currentThermalTransfer > 0.001)
            {
                this.part.AddThermalFlux(currentThermalTransfer);
                currentThermalTransfer = 0f;
                maxThermalTransfer = 0f;
            }
        }

        public override string GetInfo()
        {
            string info = string.Format("<b>-Operating Temperature:</b> {0:f1}K\n-Coolant Dump Rate: {1:f1}u/sec",
                (part.maxTemp * workingTempFactor), coolantDumpRate);

            if (coolantDumpRate > 0.001)
                info += "\n\nRight-click the radiator to dump coolant and rapidly cool the radiator.";

            return info;

        }

        public double TransferHeat(double heatToTransfer)
        {
            //If the panel isn't extended, then we cannot transfer any heat.
            if (panelState != panelStates.EXTENDED)
                return 0;

            //Are we at or exceeding max operating temp?
            if (this.part.temperature >= this.part.maxTemp * workingTempFactor)
            {
                currentThermalTransfer = 0f;
                maxThermalTransfer = 0f;
                return 0;
            }

            //Once per time-tick, calculate max thermal transfer
            if (maxThermalTransfer < 0.001f)
                maxThermalTransfer = (this.part.thermalMass * this.part.maxTemp * workingTempFactor) - (this.part.thermalMass * this.part.temperature);

            //If we can take the heat then add it to our bucket.
            if (currentThermalTransfer + heatToTransfer > maxThermalTransfer)
                currentThermalTransfer = (currentThermalTransfer + heatToTransfer) - maxThermalTransfer;
            currentThermalTransfer += heatToTransfer;

            return currentThermalTransfer;
        }
        #endregion

        #region Helpers
        public void SetRadiatorColor()
        {
            if (HighLogic.LoadedSceneIsEditor)
                return;

            //Really, *really* want to use the stock glow shader. I don't know how yet though.
            Renderer[] renderers = this.part.FindModelComponents<Renderer>();

            //Account for Draper Point
            float ratio = (float)(this.part.temperature - kGlowTempOffset) / (float)(this.part.maxTemp - kGlowTempOffset);

            if (ratio < 0.0f)
                ratio = 0f;

            //Set the emissive color
            foreach (Renderer renderer in renderers)
                renderer.material.SetColor("_EmissiveColor", new Color(ratio, ratio, ratio));

            radiatorTemperature = String.Format("{0:#.##}K", this.part.temperature);
        }

        protected void dumpCoolant(CoolantResource coolant, double dumpRate)
        {
            PartResourceDefinitionList definitions = PartResourceLibrary.Instance.resourceDefinitions;
            PartResourceDefinition resourceDef;
            double coolantToDump = dumpRate * TimeWarp.fixedDeltaTime;
            double coolantDumped = 0;
            double thermalEnergyCoolant = 0;

            //The the resource definition
            resourceDef = definitions[coolant.name];

            //Now calculate the resource amount dumped and the thermal energy of that slug of resource.
            coolantDumped = this.part.RequestResource(resourceDef.id, coolantToDump * coolant.ratio, coolant.flowMode);
            if (coolantDumped <= 0.001)
                return;
            thermalEnergyCoolant = this.part.temperature * this.part.resourceThermalMass * coolantDumped;

            //Practice conservation of energy...
            if (coolantDumped > 0.001)
                this.part.AddThermalFlux(-thermalEnergyCoolant);
        }

        protected void getCoolantNodes()
        {
            if (this.part.protoPartSnapshot != null)
            {
                if (this.part.protoPartSnapshot.partInfo != null)
                {
                    //Aha! the part's config file!
                    //Now go find the MODULE definition for ModuleRadiator
                    if (this.part.protoPartSnapshot.partInfo.partConfig != null)
                    {
                        string value;
                        ConfigNode[] moduleNodes = this.part.protoPartSnapshot.partInfo.partConfig.GetNodes("MODULE");

                        if (moduleNodes == null)
                            return;

                        //Find our module definition.
                        foreach (ConfigNode moduleNode in moduleNodes)
                        {
                            value = moduleNode.GetValue("name");
                            if (string.IsNullOrEmpty(value))
                                continue;

                            //Aha! found our module definition!
                            //Now get the coolants
                            if (value == this.ClassName)
                            {
                                CoolantResource coolant;
                                ConfigNode[] coolantResourceNodes = moduleNode.GetNodes("INPUT_RESOURCE");
                                foreach (ConfigNode node in coolantResourceNodes)
                                {
                                    coolant = new CoolantResource();
                                    coolant.name = node.GetValue("name");
                                    coolant.flowMode = (ResourceFlowMode)Enum.Parse(typeof(ResourceFlowMode), node.GetValue("flowMode"));
                                    coolant.ratio = float.Parse(node.GetValue("ratio"));
                                    coolantResources.Add(coolant);
                                }
                            }
                        }
                    }
                }
            }
        }
        #endregion
    }

}
