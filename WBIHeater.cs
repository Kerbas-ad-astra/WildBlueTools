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
Note that Wild Blue Industries is a ficticious entity 
created for entertainment purposes. It is in no way meant to represent a real entity.
Any similarity to a real entity is purely coincidental.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
namespace WildBlueIndustries
{
    public delegate void OnOverheat();
    public delegate void OnCooldown();

    public class WBIHeater : ExtendedPartModule
    {
        const float kMinimumHeatShedFactor = 0.001f;
        const float kMinimumHeatPerTick = 1.2f;

        [KSPField(isPersistant = true)]
        public bool manageHeat = false;

        [KSPField(isPersistant = true)]
        public float heatGenerated = 0f;

        [KSPField(isPersistant = true)]
        public bool heaterIsOn = false;

        public float totalHeatToShed = 0f;
        public bool isOverheated = false;

        public OnOverheat onOverheatDelegate;
        public OnCooldown onCooldownDelegate;

        private Dictionary<double, List<WBIRadiator>> categorizedRadiators = new Dictionary<double, List<WBIRadiator>>();

        public virtual void Activate()
        {
            if (isOverheated)
            {
                ScreenMessages.PostScreenMessage("System needs to cool off before restart.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            Events["ToggleHeater"].guiName = "Heat Off";
            heaterIsOn = true;
        }

        public virtual void Shutdown()
        {
            heaterIsOn = false;
            Events["ToggleHeater"].guiName = "Heat On";
        }

        [KSPAction("Toggle Heater")]
        public virtual void ToggleHeaterAction(KSPActionParam param)
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

        public override string GetInfo()
        {
            string baseInfo = base.GetInfo();

            baseInfo += "- <b>Heat generated: </b>" + heatGenerated;

            return baseInfo;
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
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

        public virtual float GenerateHeat()
        {
            //Get heat to distribute
            //If the heater is on then distribute the generated heat
            if (heaterIsOn)
                totalHeatToShed = heatGenerated;

            //Give derived classes a chance to adjust the heat to distribute
            if (heaterIsOn)
                ModTotalHeatToShed();
            if (totalHeatToShed <= 0f)
                return 0;

            return totalHeatToShed;
        }

        public virtual void ModTotalHeatToShed()
        {
        }

        public virtual void HeaterHasCooled()
        {
            isOverheated = false;
            if (onCooldownDelegate != null)
                onCooldownDelegate();
        }

        public virtual void OverheatWarning()
        {
            isOverheated = true;
            heaterIsOn = false;

            ScreenMessages.PostScreenMessage("WARNING! Heat is beyond capacity!", 5.0f, ScreenMessageStyle.UPPER_CENTER);

            if (onOverheatDelegate != null)
                onOverheatDelegate();
        }

        public virtual void ShowGui(bool isGuiVisible)
        {
            Events["ToggleHeater"].guiActive = isGuiVisible;
            Events["ToggleHeater"].guiActiveEditor = isGuiVisible;
            Actions["ToggleHeaterAction"].active = isGuiVisible;
        }

    }
}