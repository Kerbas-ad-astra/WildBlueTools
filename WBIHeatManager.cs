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
    public class WBIHeatManager : MonoBehaviour
    {
        public static List<WBIRadiator> radiators = new List<WBIRadiator>();
        public static List<WBIHeater> heaters = new List<WBIHeater>();

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
                //find all the radiators and heaters.
                if (activeVessel.Parts.Count != vesselPartCount)
                    FindHeatersAndRadiators();
            }

            //Now, manage the heat
            ManageHeat();
        }

        public void FindHeatersAndRadiators()
        {
            List<WBIRadiator> partRadiators;
            List<WBIHeater> partHeaters;

            if (activeVessel == null)
                return;
            if (activeVessel.parts.Count == vesselPartCount)
                return;

            //Set vessel part count
            vesselPartCount = activeVessel.parts.Count;

            //Clear existing radiators and heaters
            radiators.Clear();
            heaters.Clear();

            //Go through all the parts in the vessel and find any heaters and radiators
            foreach (Part part in activeVessel.parts)
            {
                partRadiators = part.FindModulesImplementing<WBIRadiator>();
                partHeaters = part.FindModulesImplementing<WBIHeater>();

                //Get all the radiator modules
                foreach (WBIRadiator radiator in partRadiators)
                    radiators.Add(radiator);

                //Get all the heater modules
                foreach (WBIHeater heater in partHeaters)
                    heaters.Add(heater);
            }

            Debug.Log("FindHeatersAndRadiators: Heaters found: " + heaters.Count + " Radiators found: " + radiators.Count);
        }

        public void ManageHeat()
        {
            if (activeVessel == null)
                return;

            //First, give the heaters a chance to generate heat. Heaters gotta heat...
            foreach (WBIHeater heater in heaters)
                heater.ManageHeat(radiators);

            //Now, give radiators a chance to shed the heat. Radiators gotta radiate...
            foreach (WBIRadiator radiator in radiators)
                radiator.ManageHeat();
        }

    }
}
