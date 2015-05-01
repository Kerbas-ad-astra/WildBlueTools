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
Wild Blue Industries is trademarked by Michael Billard and may be used for non-commercial purposes. All other rights reserved.
Note that Wild Blue Industries is a ficticious entity 
created for entertainment purposes. It is in no way meant to represent a real entity.
Any similarity to a real entity is purely coincidental.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
namespace WildBlueIndustries
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class HeatManager : MonoBehaviour
    {
        private const float kRoomTemperature = 293f;

        public List<ModuleRadiator> radiators = new List<ModuleRadiator>();
        public List<Part> nonRadiatorParts = new List<Part>();

        protected Vessel activeVessel = null;
        protected int vesselPartCount = -1;

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
                //find all the radiators and non-radiator parts.
                if (activeVessel.Parts.Count != vesselPartCount)
                {
                    ModuleRadiator radiator;
                    foreach (Part part in activeVessel.Parts)
                    {
                        radiator = part.FindModuleImplementing<ModuleRadiator>();
                        if (radiator == null)
                            nonRadiatorParts.Add(part);
                        else
                            radiators.Add(radiator);
                    }

//                    radiators = activeVessel.FindPartModulesImplementing<ModuleRadiator>();
//                    if (radiators == null)
//                        return;
                }
            }

            //Now, manage the heat
            ManageHeat();

            //Since ModuleRadiator derives from ModuleDeployableSolarPanel, we don't get a fixed update to override.
            //So we'll lend a hand here...
            foreach (ModuleRadiator radiator in radiators)
                radiator.UpdateState();
        }

        public void ManageHeat()
        {
            //Amount of thermal energy in the system, defined as thermal mass * temperature
            double partThermalEnergy = 0;

            //The part's thermal energy at room temperature
            //We use room temp so that we don't try to cool the vessel down to, say,
            //a few degrees above absolute zero.
            double partThermalAtRoomTemp = 0;

            //Maximum amount of thermal energy that may be transferred to the radiators.
            double partMaxThermalTransfer = 0f;

            //Amount of thermal energy to transfer per active radiator
            double thermalTransferPerRadiator = 0f;

            foreach (Part part in nonRadiatorParts)
            {
                //Calculate the thermal energy transfer (thermal energy = kJ/K * K = kJ)
                partThermalEnergy = part.thermalMass * part.temperature;
                partThermalAtRoomTemp = part.thermalMass * kRoomTemperature;
                partMaxThermalTransfer = partThermalEnergy - partThermalAtRoomTemp;
                thermalTransferPerRadiator = partMaxThermalTransfer / radiators.Count;

                //Now, distribute the heat to all active radiators.
                foreach (ModuleRadiator radiator in radiators)
                {
                    //If we have thermal energy to transfer, and the radiator can take the heat, then transfer the heat
                    if (partMaxThermalTransfer > 0.001 && radiator.CanTakeTheHeat(thermalTransferPerRadiator))
                    {
                        //Add the heat to the radiator
                        radiator.part.AddThermalFlux(thermalTransferPerRadiator);

                        //Transfer the heat out of the part
                        //Practice conservation of heat!
                        part.AddThermalFlux(-thermalTransferPerRadiator);
                    }
                }
            }
        }

    }
}
