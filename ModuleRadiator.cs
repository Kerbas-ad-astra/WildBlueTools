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
Wild Blue Industries is trademarked by Michael Billard and may be used for non-commercial purposes. All other rights reserved.
Note that Wild Blue Industries is a ficticious entity created for entertainment purposes. It is in no way meant to represent a real entity.
Any similarity to a real entity is purely coincidental.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
namespace WildBlueIndustries
{
    public enum CoolingCycleModes
    {
        passive,
        closed,
        open
    }

    public class ModuleRadiator : ModuleDeployableSolarPanel
    {
        //768k is when object should start glowing red, but we lower it to make the animation glow look right.
        private const float kGlowTempOffset = 600f; //My custom Draper Point...
        private const string kDefaultCoolant = "Coolant";

        [KSPField(guiActive = true, guiName = "Temperature")]
        public string radiatorTemperature;

        [KSPField(isPersistant = true)]
        public float workingTempFactor;

        [KSPField(isPersistant = true)]
        public float coolantDumpRate;

        [KSPField(isPersistant = true)]
        public CoolingCycleModes coolingCycleMode;

        [KSPField(isPersistant = true)]
        public string coolantResource = "";

        [KSPField(isPersistant = true)]
        protected string coolantValues = "";

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

            if (string.IsNullOrEmpty(coolantResource))
                coolantResource = kDefaultCoolant;
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            Fields["sunAOA"].guiActive = false;
            Fields["flowRate"].guiActive = false;

            //Set cooling mode. For now, default is closed.
            coolingCycleMode = CoolingCycleModes.closed;
            Events["ToggleCoolingMode"].guiName = "Cooling Mode (closed)";
        }

        public void UpdateState()
        {
            if (coolingCycleMode == CoolingCycleModes.open)
            {
                double coolantToDump = coolantDumpRate * TimeWarp.fixedDeltaTime;
                double coolantDumped = 0;
                double thermalEnergyCoolant = 0;

                coolantDumped = this.part.RequestResource(coolantResource, coolantToDump);
                thermalEnergyCoolant = this.part.temperature * this.part.resourceThermalMass * coolantDumped;

                if (coolantDumped > 0.001)
                    this.part.AddThermalFlux(-thermalEnergyCoolant);
            }

            SetRadiatorColor();
        }

        public override string GetInfo()
        {
            string info = string.Format("<b>-Operating Temperature:</b> {0:f1}K\n-Coolant Dump Rate: {1:f1}u/sec",
                (part.maxTemp * workingTempFactor), coolantDumpRate);

            return info;

        }

        public bool CanTakeTheHeat(double heatToTransfer)
        {
            double thermalEnergy = this.part.thermalMass * this.part.maxTemp * workingTempFactor;

            if (thermalEnergy >= heatToTransfer)
                return true;
            else
                return false;
        }
        #endregion

        #region Helpers
        public void SetRadiatorColor()
        {
            if (HighLogic.LoadedSceneIsEditor)
                return;

            Renderer[] renderers = this.part.FindModelComponents<Renderer>();
            float ratio = (float)(this.part.temperature - kGlowTempOffset) / (float)(this.part.maxTemp - kGlowTempOffset);

            if (ratio < 0.0f)
                ratio = 0f;

            //Set the emissive color
            foreach (Renderer renderer in renderers)
                renderer.material.SetColor("_EmissiveColor", new Color(ratio, ratio, ratio));

            radiatorTemperature = String.Format("{0:#.##}K", this.part.temperature);
        }
        #endregion
    }

}
