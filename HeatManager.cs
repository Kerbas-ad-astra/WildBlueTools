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

Portions of this software use code from the Firespitter plugin by Snjo, used with permission. Thanks Snjo for sharing how to switch meshes. :)

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
namespace WildBlueIndustries
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class HeatManager : MonoBehaviour
    {
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
                    radiators = activeVessel.FindPartModulesImplementing<ModuleRadiator>();
                }
            }

            //Now, manage the heat
            ManageHeat();
        }

        public void ManageHeat()
        {
            //Amount of thermal energy in the system, defined as thermal mass * temperature
            double partThermalEnergy = 0;

            //The part's thermal energy at ambient temperature
            double partThermalAtAmbient = 0;

            //Maximum amount of thermal energy that may be transferred to the radiators.
            double partMaxThermalTransfer = 0f;

            //Amount of thermal energy to transfer per active radiator (defined as a radiator that is extended and working)
            double thermalTransferPerRadiator = 0f;

            foreach (Part part in activeVessel.parts)
            {
                //Calculate the thermal energy transfer
                partThermalEnergy = part.thermalMass * part.temperature;
                partThermalAtAmbient = part.thermalMass * part.externalTemperature;
                partMaxThermalTransfer = partThermalEnergy - partThermalAtAmbient;
                thermalTransferPerRadiator = partMaxThermalTransfer / radiators.Count;

                //TODO: Account for crew capacity

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

                    //Since ModuleRadiator derives from ModuleDeployableSolarPanel, we don't get a fixed update to override.
                    //So we'll lend a hand here...
                    radiator.UpdateState();
                }
            }
        }

    }
}
