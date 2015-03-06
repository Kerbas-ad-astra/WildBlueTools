using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;

/*
Source code copyrighgt 2014, by Michael Billard (Angel-125)
License: CC BY-NC-SA 4.0
License URL: https://creativecommons.org/licenses/by-nc-sa/4.0/
Wild Blue Industries is trademarked by Michael Billard and may be used for non-commercial purposes. All other rights reserved.
Note that Wild Blue Industries is a ficticious entity 
created for entertainment purposes. It is in no way meant to represent a real entity.
Any similarity to a real entity is purely coincidental.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
namespace WildBlueIndustries
{
    public class WBIHeater : ExtendedPartModule
    {
        const float kMinimumHeatShedFactor= 0.001f;

        [KSPField(isPersistant = true)]
        public bool manageHeat = false;

        [KSPField(isPersistant = true)]
        public float heatGenerated = 0f;

        public static List<WBIRadiator> radiators = new List<WBIRadiator>();
        public static int vesselPartCount = -1;

        public bool heaterIsOn = false;
        public float totalHeatToShed = 0f;
        public bool isOverheated = false;

        [KSPAction("Toggle Heater")]
        public void ToggleHeaterAction(KSPActionParam param)
        {
            ToggleHeater();
        }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Toggle Heat", active = true, externalToEVAOnly = false, unfocusedRange = 3.0f, guiActiveUnfocused = true)]
        public virtual void ToggleHeater()
        {
            if (isOverheated)
            {
                ScreenMessages.PostScreenMessage("System needs to cool off before restart.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            heaterIsOn = !heaterIsOn;

            if (heaterIsOn)
            {
                Events["ToggleHeater"].guiName = "Heat Off";
            }

            else
            {
                Events["ToggleHeater"].guiName = "Heat On";
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (HighLogic.LoadedSceneIsFlight)
                vesselPartCount = -1;
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            Events["ToggleHeater"].guiName = "Heat On";
        }

        public override void OnFixedUpdate()
        {
            base.OnFixedUpdate();
            if (manageHeat == false)
                return;
            FindRadiators();

            PartResource heatSink = this.part.Resources["SystemHeat"];
            float heatRemaining = 0f;
            float heaterShedRate = heatGenerated * kMinimumHeatShedFactor;
            float heatToDistribute = 0f;

            //Get heat to distribute
            //If the heater is on then distribute the generated heat
            if (heaterIsOn)
                totalHeatToShed = heatGenerated;

            //If the heater is off, then distribute the heat stored in the heatsink.
            else if (heatSink.amount > 0)
                totalHeatToShed = (float)heatSink.amount;

            //Nothing to distribute
            else
                totalHeatToShed = 0f;

            //Give derived classes a chance to adjust the heat to distribute
            if (!isOverheated && heaterIsOn)
                ModTotalHeatToShed();
            if (totalHeatToShed <= 0f)
                return;

            //Calculate the heat to distribute amongst the radiators
            //Heat generated is calibrated to Time.fixedDeltaTime instead of TimeWarp.fixedDeltaTime.
            //Whether we are at 1x time or 100000x time, the rate of change stays relative.
            //Plus it avoids the headaches with timewarp...
            totalHeatToShed = totalHeatToShed * Time.fixedDeltaTime;
            if (radiators.Count > 0)
                heatToDistribute = totalHeatToShed / (float)radiators.Count;
            
            //If we have heat in the heat sink, make sure to distribute the minimum level of heat
            if (heatSink.amount > 0f && heatToDistribute < heaterShedRate)
                heatToDistribute = heaterShedRate;

            //Heater is off, see if we can shed our own internal heat
            if (heaterIsOn == false)
            {
                //Threshold to make sure we kill all the heat
                if (heatSink.amount <= heaterShedRate)
                {
                    heatToDistribute = 0f;
                    heatSink.amount = 0f;
                    isOverheated = false;
                    HeaterHasCooled();
                    return;
                }

                heatSink.amount -= heatToDistribute;
            }

            //Distribute the heat
            if (radiators.Count == 0)
                heatRemaining = heatToDistribute;
            foreach (WBIRadiator radiator in radiators)
                heatRemaining += radiator.TransferHeat(heatToDistribute);

            //If we didn't shed any heat then don't update the heat sink.
            if (isOverheated && heatRemaining >= heatToDistribute)
                return;

            //If we have heat remaining then dump it into our own heat sink
            if (heatRemaining > 0f)
            {
                //If we've pretty much bottomed out the heat sink then we're no longer overheated.
                if (isOverheated && heatSink.amount <= heatToDistribute)
                {
                    heatSink.amount = 0.0f;
                    isOverheated = false;
                }

                //If we have heat remaining and it won't overload the heat sink then add the remaining heat
                if (isOverheated ==  false && (heatSink.amount + heatRemaining <= heatSink.maxAmount))
                {
                    heatSink.amount += heatRemaining;
                }

                //We are overheating!
                else if (isOverheated == false)
                {
                    heatSink.amount += heatRemaining;
                    isOverheated = true;
                    heaterIsOn = false;
                    OverheatWarning();
                }
            }

        }

        public virtual void ModTotalHeatToShed()
        {
        }

        public virtual void HeaterHasCooled()
        {
        }

        public virtual void OverheatWarning()
        {
            ScreenMessages.PostScreenMessage("WARNING! Heat is beyond capacity!", 5.0f, ScreenMessageStyle.UPPER_CENTER);
        }

        public virtual void FindRadiators()
        {
            List<WBIRadiator> partRadiators;
            if (this.part.vessel.parts.Count == vesselPartCount)
                return;

            vesselPartCount = this.part.vessel.parts.Count;
            radiators.Clear();

            foreach (Part part in this.part.vessel.parts)
            {
                if (part == this.part)
                    continue;

                partRadiators = part.FindModulesImplementing<WBIRadiator>();

                foreach (WBIRadiator radiator in partRadiators)
                    radiators.Add(radiator);
            }
        }

    }
}
