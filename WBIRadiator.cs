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
    public class WBIRadiator : ModuleDeployableSolarPanel
    {
        [KSPField(guiActive = true, guiName = "State: ")]
        public string currentState;

        [KSPField(isPersistant = true)]
        public float heatCapacity;

        [KSPField(isPersistant = true)]
        public float coolantMass;

        [KSPField(isPersistant = true)]
        public float radiatorArea;

        [KSPField(isPersistant = true)]
        public float radiatorAreaStowed;

        [KSPField(isPersistant = true)]
        public float emissivity;

        float constEmissArea;
        float shcCoolantMass;

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

            Fields["sunAOA"].guiActive = false;
            Fields["flowRate"].guiActive = false;

            if (panelState == panelStates.EXTENDED)
                constEmissArea = Utils.StefanBoltzmann * emissivity * radiatorArea;
            else
                constEmissArea = Utils.StefanBoltzmann * emissivity * radiatorAreaStowed;

            //Coolant mass is in metric tons, convert it to kilograms
            shcCoolantMass = heatCapacity * (coolantMass * 1000f);
        }

        public virtual void ManageHeat()
        {
            RadiateHeat();

            SetRadiatorColor();

            if (panelState == panelStates.EXTENDED)
            {
                Fields["currentState"].guiName = "Temperature";
                currentState = String.Format("{0:#.##}c", this.part.temperature);
            }
            else
            {
                Fields["currentState"].guiName = "State";
                currentState = status;
            }
        }

        public void SetRadiatorColor()
        {
            if (HighLogic.LoadedSceneIsEditor)
                return;

            Renderer[] renderers = this.part.FindModelComponents<Renderer>();
            float heatRatio = this.part.temperature / this.part.maxTemp;

            if (this.part.temperature < 0f)
                heatRatio = 0f;

            //Set the emissive color
            foreach (Renderer renderer in renderers)
                renderer.material.SetColor("_EmissiveColor", new Color(heatRatio, heatRatio, heatRatio));
        }

        public void RadiateHeat()
        {
            if (panelState == panelStates.EXTENDED)
                constEmissArea = Utils.StefanBoltzmann * emissivity * radiatorArea;
            else
                constEmissArea = Utils.StefanBoltzmann * emissivity * radiatorAreaStowed;

            //The amount of heat radiated in joules is calculated using the Stefan-Boltzmann law
            float heatRadiated = constEmissArea * Mathf.Pow(this.part.temperature + Utils.CelsiusToKelvin, 4.0f);
            PartResource heatSink = this.part.Resources["SystemHeat"];

            //Dump heat from the heat sink, which is rated in terms of megajoules
            heatSink.amount -= heatRadiated / 1000000f;
            if (heatSink.amount < 0.1f)
            {
                heatSink.amount = 0f;
                return;
            }

            //Now calculate the temperature decrease
            float temperatureDecrease = (float)(heatSink.amount * 1000000f) / shcCoolantMass;
            float currentTemperature = this.part.temperature + Utils.CelsiusToKelvin;
            float newTemperature = currentTemperature + temperatureDecrease;

            this.part.temperature = newTemperature - Utils.CelsiusToKelvin;
        }

        public float TransferHeat(float transferAmount)
        {
            PartResource heatSink = this.part.Resources["SystemHeat"];

            //Broken panel? no heat transfer
            if (panelState == panelStates.BROKEN)
                return transferAmount;

            //If the heat sink is full, no transfer
            if (heatSink.amount == heatSink.maxAmount)
                return transferAmount;

            //Temperature increase is based upon the specific heat capacity of the material and the mass of the material
            //Heat input is converted to Joules
            float temperatureIncrease = (transferAmount * 1000000f) / shcCoolantMass;
            float currentTemperature = this.part.temperature + Utils.CelsiusToKelvin;
            float newTemperature = currentTemperature + temperatureIncrease;
            float heatRemaining = 0;

            //Safety feature: If adding the heat would exceed the radiator's temperature, then reject the transfer.
            if (newTemperature - Utils.CelsiusToKelvin >= this.part.maxTemp)
                return transferAmount;

            this.part.temperature = newTemperature - Utils.CelsiusToKelvin;

            //Make sure we haven't maxed out the heat sink
            if (heatSink.amount + transferAmount <= heatSink.maxAmount)
            {
                heatSink.amount += transferAmount;
            }

            else
            {
                heatRemaining = (float)((heatSink.amount + transferAmount) - heatSink.maxAmount);
                heatSink.amount = heatSink.maxAmount;
            }

            return heatRemaining;
        }
    }
}
