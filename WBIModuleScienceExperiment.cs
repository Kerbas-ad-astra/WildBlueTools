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
    public delegate void ExperimentTransferedEvent(WBIModuleScienceExperiment transferedExperiment);
    public delegate void TransferReceivedEvent(WBIModuleScienceExperiment transferRecipient);

    public class WBIModuleScienceExperiment : ModuleScienceExperiment
    {
        [KSPField(isPersistant = true)]
        public string overrideExperimentID;

        [KSPField]
        public string requiredParts;

        [KSPField]
        public int minCrew;

        [KSPField]
        public string celestialBodies;

        [KSPField]
        public double minAltitude;

        [KSPField]
        public double maxAltitude;

        [KSPField]
        public string requiredResources;

        [KSPField]
        public string defaultExperiment = "WBIEmptyExperiment";

        [KSPField]
        public string status;

        [KSPField(isPersistant = true)]
        public bool isGUIVisible;

        [KSPField]
        public string situations;

        [KSPField(isPersistant = true)]
        public bool isCompleted;

        public event ExperimentTransferedEvent onExperimentTransfered;
        public event TransferReceivedEvent onExperimentReceived;

        protected int currentPartCount;
        protected bool hasRequiredParts;
        protected Dictionary<string, double> resourceMap = null;

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (!string.IsNullOrEmpty(overrideExperimentID))
                LoadFromDefinition(overrideExperimentID);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            SetGUIVisible(isGUIVisible);

            //Required resources
            if (string.IsNullOrEmpty(requiredResources) == false)
            {
                //Build resource map
                string[] resources = requiredResources.Split(new char[] { ';' });
                string[] resourceAmount = null;
                resourceMap = new Dictionary<string, double>();
                foreach (string resource in resources)
                {
                    resourceAmount = resource.Split(new char[] { ',' });
                    resourceMap.Add(resourceAmount[0], double.Parse(resourceAmount[1]));
                }
            }
        }

        public void SetGUIVisible(bool guiVisible)
        {
            isGUIVisible = guiVisible;
            Events["DeployExperiment"].guiActive = guiVisible;
            Events["DeployExperimentExternal"].guiActiveUnfocused = guiVisible;
        }

        public bool CheckCompletion()
        {
            if (HighLogic.LoadedSceneIsFlight == false)
                return false;

            //Mininum Crew
            if (minCrew > 0)
            {
                if (this.part.vessel.GetCrewCount() < minCrew)
                {
                    status = "Needs " + minCrew.ToString() + " crew";
                    return false;
                }
            }

            //Celestial bodies
            if (string.IsNullOrEmpty(celestialBodies) == false)
            {
                if (celestialBodies.Contains(this.part.vessel.mainBody.name) == false)
                {
                    status =  "Needs one: " + celestialBodies;
                    return false;
                }
            }

            //Flight states
            if (string.IsNullOrEmpty(situations) == false)
            {
                string situation = this.part.vessel.situation.ToString();
                if (situations.Contains(situation) == false)
                {
                    status = "Needs one: " + situations;
                    return false;
                }
            }

            //Min altitude
            if (minAltitude > 0.001f)
            {
                if (this.part.vessel.altitude < minAltitude)
                {
                    status = string.Format("Min altitude: {0:f2}m", minAltitude);
                    return false;
                }
            }

            //Max altitude
            if (maxAltitude > 0.001f)
            {
                if (this.part.vessel.altitude > maxAltitude)
                {
                    status = string.Format("Max altitude: {0:f2}m", maxAltitude);
                    return false;
                }
            }

            //Required parts
            if (string.IsNullOrEmpty(requiredParts) == false)
            {
                int partCount = this.part.vessel.parts.Count<Part>();
                if (currentPartCount != partCount)
                {
                    currentPartCount = partCount;
                    hasRequiredParts = false;
                    foreach (Part vesselPart in this.part.vessel.parts)
                    {
                        if (requiredParts.Contains(vesselPart.partInfo.title))
                        {
                            hasRequiredParts = true;
                            break;
                        }
                    }
                    if (hasRequiredParts == false)
                    {
                        status = "Needs " + requiredParts;
                        return false;
                    }
                }

                else if (hasRequiredParts == false)
                {
                    status = "Needs " + requiredParts;
                    return false;
                }
            }

            //Required resources
            if (string.IsNullOrEmpty(requiredResources) == false)
            {
                //for each resource, see if we still need more
                foreach (PartResource resource in this.part.Resources)
                {
                    if (resourceMap.ContainsKey(resource.resourceName))
                    {
                        if (resource.amount < resource.maxAmount)
                        {
                            status = "Needs more " + resource.resourceName;
                            return false;
                        }
                    }
                }
            }

            //AOK
            isCompleted = true;
            status = "Completed";
            return true;
        }

        public void TransferExperiment(WBIModuleScienceExperiment sourceExperiment)
        {
            //Load parameters from experiment definition
            LoadFromDefinition(sourceExperiment.experimentID);

            //Now set the source experiment to a dummy experiment
            sourceExperiment.LoadFromDefinition(defaultExperiment);

            //Let listeners know
            if (onExperimentReceived != null)
                onExperimentReceived(this);
            if (sourceExperiment.onExperimentTransfered != null)
                sourceExperiment.onExperimentTransfered(sourceExperiment);
        }

        public void LoadFromDefinition(string experimentIDCode)
        {
            ConfigNode[] experiments = GameDatabase.Instance.GetConfigNodes("EXPERIMENT_DEFINITION");
            ConfigNode nodeDefinition = null;

            //Find our desired experiment
            foreach (ConfigNode nodeExperiment in experiments)
            {
                if (nodeExperiment.HasValue("id"))
                {
                    if (nodeExperiment.GetValue("id") == experimentIDCode)
                    {
                        nodeDefinition = nodeExperiment;
                        break;
                    }
                }
            }
            if (nodeDefinition == null)
            {
                Debug.Log("loadFromDefinition - unable to find the experiment definition for " + experimentIDCode);
                return;
            }

            //Now load the parameters
            experimentID = experimentIDCode;
            overrideExperimentID = experimentID;
            experiment = ResearchAndDevelopment.GetExperiment(experimentID);

            if (nodeDefinition.HasValue("experimentActionName"))
                experimentActionName = nodeDefinition.GetValue("experimentActionName");
            else
                experimentActionName = "Get Results";
            Events["DeployExperiment"].guiName = experimentActionName;
            Events["DeployExperimentExternal"].guiName = experimentActionName;

            if (nodeDefinition.HasValue("resetActionName"))
                resetActionName = nodeDefinition.GetValue("resetActionName");
            else
                resetActionName = "Reset Experiment";

            if (nodeDefinition.HasValue("reviewActionName"))
                reviewActionName = nodeDefinition.GetValue("reviewActionName");
            else
                reviewActionName = "Review Results";

            if (nodeDefinition.HasValue("collectActionName"))
                collectActionName = nodeDefinition.GetValue("collectActionName");
            else
                collectActionName = "Collect Data";

            /*
            if (nodeDefinition.HasValue("useStaging"))
                useStaging = bool.Parse(nodeDefinition.GetValue("useStaging"));
            else
                useStaging = false;

            if (nodeDefinition.HasValue("useActionGroups"))
                useActionGroups = bool.Parse(nodeDefinition.GetValue("useActionGroups"));
            else
                useActionGroups = false;

            if (nodeDefinition.HasValue("dataIsCollectable"))
                dataIsCollectable = bool.Parse(nodeDefinition.GetValue("xmitDataScalar"));
            else
                dataIsCollectable = false;

            if (nodeDefinition.HasValue("interactionRange"))
                interactionRange = float.Parse(nodeDefinition.GetValue("interactionRange"));
            else
                interactionRange = 1.2f;

            if (nodeDefinition.HasValue("xmitDataScalar"))
                xmitDataScalar = float.Parse(nodeDefinition.GetValue("xmitDataScalar"));
            else
                xmitDataScalar = 0.05f;

            if (nodeDefinition.HasValue("hideUIwhenUnavailable"))
                hideUIwhenUnavailable = bool.Parse(nodeDefinition.GetValue("hideUIwhenUnavailable"));
            else
                hideUIwhenUnavailable = true;

            if (nodeDefinition.HasValue("resettable"))
                resettable = bool.Parse(nodeDefinition.GetValue("resettable"));
            else
                resettable = false;

            if (nodeDefinition.HasValue("resettableOnEVA"))
                resettableOnEVA = bool.Parse(nodeDefinition.GetValue("resettableOnEVA"));
            else
                resettableOnEVA = false;

            if (nodeDefinition.HasValue("rerunnable"))
                rerunnable = bool.Parse(nodeDefinition.GetValue("rerunnable"));
            else
                rerunnable = false;

            if (nodeDefinition.HasValue("resourceResetCost"))
                resourceResetCost = float.Parse(nodeDefinition.GetValue("resourceResetCost"));

            if (nodeDefinition.HasValue("resourceToReset"))
                resourceToReset = nodeDefinition.GetValue("resourceToReset");
            */

            //Dirty the GUI
            MonoUtilities.RefreshContextWindows(this.part);
        }
    }
}
