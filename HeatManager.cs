using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;

/*
Source code copyrighgt 2015, by Michael Billard (Angel-125)
License: CC BY-NC-SA 4.0
License URL: https://creativecommons.org/licenses/by-nc-sa/4.0/
If you want to use this code, give me a shout on the KSP forums! :)
Wild Blue Industries is trademarked by Michael Billard and may be used for non-commercial purposes. All other rights reserved.
Note that Wild Blue Industries is a ficticious entity 
created for entertainment purposes. It is in no way meant to represent a real entity.
Any similarity to a real entity is purely coincidental.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
namespace WildBlueIndustries
{
    public class PartTargetTemp
    {
        public Part part;
        public double targetTemp;
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class HeatManager : MonoBehaviour
    {
        private const float kRoomTemperature = 293;

        public List<ModuleRadiator> radiators = new List<ModuleRadiator>();
        public List<PartTargetTemp> partTargetTemps = new List<PartTargetTemp>();

        protected Vessel activeVessel = null;
        protected int vesselPartCount = -1;

        //By default, the target temperature is 49% of the part's maximum temperature.
        //This is just under the game's temperature warning gauge.
        //This value can be configured in the Cooldown config file.
        protected float maxTempPercent = 0.49f;

        public void Start()
        {
            string value;
            ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("COOLDOWN");
            if (nodes != null)
            {
                value = nodes[0].GetValue("maxTempPercent");
                if (string.IsNullOrEmpty(value) == false)
                    maxTempPercent = float.Parse(value);
            }
        }

        public void FixedUpdate()
        {
            //The manager is only usable during flight.
            if (HighLogic.LoadedSceneIsFlight == false)
                return;

            //Get active vessel
            if (FlightGlobals.ActiveVessel != activeVessel)
            {
                activeVessel = FlightGlobals.ActiveVessel;
                if (activeVessel == null)
                    return;

                //If the part count has changed, such as when the vessel breaks up, docks, or undocks, then 
                //find all the radiators and non-radiator parts and their target temperatures.
                FindPartTemperatures();
            }

            //Now, manage the heat
            ManageHeat();

            //Since ModuleRadiator derives from ModuleDeployableSolarPanel, we don't get a fixed update to override.
            //So we'll lend a hand here...
            foreach (ModuleRadiator radiator in radiators)
                radiator.UpdateState();
        }

        public void FindPartTemperatures()
        {
            if (activeVessel.Parts.Count != vesselPartCount)
            {
                vesselPartCount = activeVessel.Parts.Count;
                radiators.Clear();
                partTargetTemps.Clear();

                ModuleRadiator radiator;
                foreach (Part part in activeVessel.Parts)
                {
                    //If the part has a radiator then add it to the radiator's list.
                    radiator = part.FindModuleImplementing<ModuleRadiator>();
                    if (radiator != null)
                    {
                        radiators.Add(radiator);
                        continue;
                    }

                    //It isn't a radiator, if it's physicsless then keep going
                    if (part.PhysicsSignificance == 1)
                        continue;

                    //Create a new PartTargetTemp
                    PartTargetTemp partTargetTemp = new PartTargetTemp();
                    partTargetTemps.Add(partTargetTemp);
                    partTargetTemp.part = part;

                    //If the part has a ModuleTargetTemp then use its temperature

                    //If the part is crewed then the target temperature is room temperature.
                    if (part.CrewCapacity > 0)
                        partTargetTemp.targetTemp = kRoomTemperature;

                    //By default, the part will be kept at a percentage of its maximum temperature.
                    //This can be configured in the Cooldown config file
                    else
                        partTargetTemp.targetTemp = part.maxTemp * maxTempPercent;
                }
            }
        }

        public void ManageHeat()
        {
            //Amount of thermal energy in the system, defined as thermal mass * temperature
            double partThermalEnergy = 0;

            //Thermal energy at the target temperature.
            //Some parts may run hot, others want to stay cool.
            //Either way we want to make sure that the part doesn't get too cold.
            double partThermalAtTargetTemp = 0;

            //Total amount of thermal energy that may be transferred to the radiators.
            double partThermalTransfer = 0f;

            //Amount of thermal energy to transfer per active radiator
            double thermalTransferPerRadiator = 0f;

            foreach (PartTargetTemp partTargetTemp in partTargetTemps)
            {
                //Calculate the thermal energy transfer (thermal energy = kJ/K * K = kJ)
                partThermalEnergy = partTargetTemp.part.thermalMass * partTargetTemp.part.temperature;
                partThermalAtTargetTemp = partTargetTemp.part.thermalMass * partTargetTemp.targetTemp;
                partThermalTransfer = partThermalEnergy - partThermalAtTargetTemp;
                thermalTransferPerRadiator = partThermalTransfer / radiators.Count;

                if (thermalTransferPerRadiator < 0.001f)
                    continue;

                //Now, distribute the heat to all active radiators.
                partThermalTransfer = 0f; //We'll use this to know how much heat was actually transferred.
                foreach (ModuleRadiator radiator in radiators)
                    partThermalTransfer += radiator.TransferHeat(thermalTransferPerRadiator);

                //Transfer the heat out of the part
                //Practice conservation of heat!
                if (partThermalTransfer > 0.001f)
                    partTargetTemp.part.AddThermalFlux(-partThermalTransfer);
            }
        }

    }
}
