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
    public enum ERadiatorStates
    {
        Idle,
        HeatTransferHeating,
        HeatTransferCooling,
        CoolingOff
    }

    public class WBIRadiator : ExtendedPartModule
    {
        public const float kDefaultRadiatorCapacity = 105f;

//        [KSPField(guiActive = true, guiName = "Temperature")]
//        public string radiatorTemperature;

        [KSPField(guiActive = true, guiName = "Temperature")]
        public string currentState;

        [KSPField(isPersistant = true)]
        public float radiatorCapacity;

        [KSPField(isPersistant = true)]
        public float passiveRadiatorCapacity;

        [KSPField(isPersistant = true)]
        float targetHeat;

        [KSPField(isPersistant = true)]
        ERadiatorStates radiatorState;

        [KSPField(isPersistant = true)]
        float curLerp = 0f;

        [KSPField(isPersistant = true)]
        float lerpHeatRate = 0.1f;

        [KSPField(isPersistant = true)]
        float lerpCoolRate = 0.03f;

        [KSPField(isPersistant = true)]
        float curHeat = 0f;

        [KSPField(isPersistant = true)]
        float startingHeat = 0f;

        ModuleDeployableSolarPanel solarPanel;

        public virtual bool isActive
        {
            get
            {
                if (solarPanel == null)
                    return true;

                else if (solarPanel.panelState == ModuleDeployableSolarPanel.panelStates.EXTENDED)
                    return true;

                else
                    return false;
            }
        }

        public bool hasCooledOff
        {
            get
            {
                if (curHeat <= 0.001f)
                {
                    radiatorState = ERadiatorStates.Idle;
                    curLerp = 0f;
                    targetHeat = 0f;
                    return true;
                }

                return false;
            }
        }

        public override string GetInfo()
        {
            string baseInfo = base.GetInfo();

            baseInfo += "- <b>Radiator capacity (deployed): </b>" + radiatorCapacity;
            baseInfo += "\n- <b>Radiator capacity (stowed): </b>" + passiveRadiatorCapacity;

            return baseInfo;
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            /*
            //Remove the solar panel info
            foreach (AvailablePart.ModuleInfo mi in part.partInfo.moduleInfos)
                Debug.Log("FRED module title: " + mi.moduleName);

            if (part.partInfo != null)
                if (part.partInfo.moduleInfos != null)
                    part.partInfo.moduleInfos.RemoveAll(modi => modi.moduleName == "Deployable Solar Panel");
             */
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            if (radiatorCapacity == 0f)
                radiatorCapacity = kDefaultRadiatorCapacity;

            solarPanel = this.part.FindModuleImplementing<ModuleDeployableSolarPanel>();
            if (solarPanel != null)
            {
                solarPanel.Fields["sunAOA"].guiActive = false;
                solarPanel.Fields["flowRate"].guiActive = false;
            }

            if (curHeat == targetHeat)
                radiatorState = ERadiatorStates.Idle;
        }

        public override void OnFixedUpdate()
        {
            base.OnFixedUpdate();
            SetRadiatorColor();
        }

        public void SetRadiatorColor()
        {
            if (HighLogic.LoadedSceneIsEditor)
                return;

            Renderer[] renderers = this.part.FindModelComponents<Renderer>();
            float ratio = (float)(this.part.temperature - 600.0f) / (float)(this.part.maxTemp - 600.0f);

            if (ratio < 0.0f)
                ratio = 0f;

            //Set the emissive color
            //768k is when they should start glowing red
            foreach (Renderer renderer in renderers)
                renderer.material.SetColor("_EmissiveColor", new Color(ratio, ratio, ratio));

            currentState = String.Format("{0:#.##}K", this.part.temperature);
        }

        public float RadiateHeat()
        {
            return 0;
            calculateCurrentHeat();

            SetRadiatorColor();

            setRadiatorState();

            if (this.isActive)
                return radiatorCapacity;

            else
                return passiveRadiatorCapacity;
        }

        public float TransferHeat(float transferAmount)
        {
            return 0;
            float heatToTransfer = transferAmount;
            float heatCapacity;

            if (this.isActive)
                heatCapacity = radiatorCapacity;
            else
                heatCapacity = passiveRadiatorCapacity;

            if (radiatorState == ERadiatorStates.CoolingOff)
                return radiatorCapacity;

            if (heatToTransfer > heatCapacity)
                heatToTransfer = heatCapacity;

            if (heatToTransfer == targetHeat)
                return heatToTransfer;

            targetHeat = transferAmount;
            startingHeat = curHeat;
            curLerp = 0f;

            if (targetHeat > startingHeat)
                radiatorState = ERadiatorStates.HeatTransferHeating;

            else if (targetHeat < startingHeat)
                radiatorState = ERadiatorStates.HeatTransferCooling;

            return heatToTransfer;
        }

        #region Helpers
        protected void calculateCurrentHeat()
        {
            if (radiatorState == ERadiatorStates.Idle)
                return;

            //Calculate current heat via lerp.
            if (radiatorState == ERadiatorStates.HeatTransferHeating)
                curLerp += Time.fixedDeltaTime * lerpHeatRate;
            else if (this.isActive)
                curLerp += Time.fixedDeltaTime * lerpCoolRate;
            else
                curLerp += Time.fixedDeltaTime * lerpCoolRate * 0.1f;

            if (curLerp > 1.0f)
                curLerp = 1.0f;

            curHeat = Mathf.Lerp(startingHeat, targetHeat, curLerp);

            if (curHeat == targetHeat)
                radiatorState = ERadiatorStates.Idle;

            if (curHeat < 0.001f)
            {
                curHeat = 0;
                radiatorState = ERadiatorStates.Idle;
            }
        }

        protected void setRadiatorState()
        {
            switch (radiatorState)
            {
                case ERadiatorStates.HeatTransferHeating:
                    currentState = "Heating up";
                    break;

                case ERadiatorStates.HeatTransferCooling:
                    currentState = "Cooling down";
                    break;

                case ERadiatorStates.CoolingOff:
                    currentState = string.Format("Overheated {0:f2}%", (1 - curLerp) * 100);
                    break;

                default:
                    if (this.isActive && curHeat > 0.001f)
                        currentState = "Radiating";
                    else
                        currentState = "Ready";
                    break;
            }

            //radiatorTemperature = String.Format("{0:#.##}C", this.part.temperature);
        }
        #endregion
    }
}
