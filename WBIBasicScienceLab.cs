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
    public struct ResearchResource
    {
        public string name;
        public float amount;
        public ResourceFlowMode flowMode;
    }

    public class WBIBasicScienceLab : ExtendedPartModule
    {
        private const float kBaseSuccess = 80f;
        private const float kCriticalSuccess = 95f;
        private const float kCriticalFailure = 33f;
        private const float kCriticalFailPenalty = 15f;
        private const float kBaseResearchDivisor = 1.75f;
        private const float kDefaultResearchTime = 7f;
        private const string kBotchedResults = "Botched results! This may have consequences in the future...";
        private const string kGreatResults = "Analysis better than expected!";
        private const string kGoodResults = "Good results!";
        private const string kNoSuccess = "Analysis inconclusive";
        private const string kInsufficientResources = "Research halted. Not enough resources to perform the analysis.";
        private const float kCriticalSuccessBonus = 1.5f;

        public static bool showResults = true;

        [KSPField]
        public string startResearchGUIName;

        [KSPField]
        public string stopResearchGUIName;

        [KSPField(isPersistant = true)]
        public float scientistBonus;

        [KSPField(isPersistant = true)]
        public double researchTime;

        [KSPField(isPersistant = true)]
        public float researchChance;

        [KSPField(isPersistant = true)]
        public float sciencePerCycle;

        [KSPField(isPersistant = true)]
        public bool isResearching;

        [KSPField(isPersistant = true)]
        public double lastUpdated;

        [KSPField(isPersistant = true)]
        public double researchStartTime;

        [KSPField(guiActive = true, guiName = "Progress")]
        public string researchProgress;

        public double elapsedTime;
        public List<ResearchResource> inputResources = new List<ResearchResource>();

        protected float averageCrewSkill = -1.0f;
        protected double secondsPerCycle = 0f;
        protected bool failedLastAttempt;

        #region Actions And Events
        [KSPAction("Toggle Research")]
        public virtual void ToggleResearchAction(KSPActionParam param)
        {
            ToggleResearch();
        }

        [KSPEvent(guiActive = true)]
        public virtual void ToggleResearch()
        {
            isResearching = !isResearching;

            if (isResearching)
            {
                Events["ToggleResearch"].guiName = stopResearchGUIName;
                researchStartTime = Planetarium.GetUniversalTime();
            }

            else
            {
                Events["ToggleResearch"].guiName = startResearchGUIName;
            }
        }

        #endregion

        #region Overrides

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (HighLogic.LoadedSceneIsFlight == false)
                return;

            //Find our part module and load what we need
            LoadValuesFromNode(node);
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
        }

        public override void OnFixedUpdate()
        {
            base.OnFixedUpdate();

            if (HighLogic.LoadedSceneIsFlight == false)
                return;
            if (isResearching == false)
                return;

            //Calculate the average crew skill and seconds of research per cycle.
            //Thes values can change if the player swaps out crew.
            averageCrewSkill = GetAverageSkill();
            secondsPerCycle = GetSecondsPerCycle();

            //Calculate elapsed time
            elapsedTime = Planetarium.GetUniversalTime() - researchStartTime;

            //Consume input resources
            ConsumeResources();

            //Calculate progress
            CalculateProgress();

            //If we've completed our research cycle then perform the analyis.
            if (elapsedTime >= secondsPerCycle)
            {
                PerformAnalysis();
                
                //Reset elapsed time.
                researchStartTime = Planetarium.GetUniversalTime();
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            //Setup
            researchProgress = "None";
            if (researchTime == 0f)
                researchTime = kDefaultResearchTime;
            if (isResearching)
                Events["ToggleResearch"].guiName = stopResearchGUIName;
            else
                Events["ToggleResearch"].guiName = startResearchGUIName;
        }

        #endregion

        public void SetGuiVisible(bool isVisible)
        {
            Fields["researchProgress"].guiActive = isVisible;
            Fields["researchProgress"].guiActiveEditor = isVisible;
            Events["ToggleResearch"].guiActive = isVisible;
            Events["ToggleResearch"].guiActiveUnfocused = isVisible;
            Events["ToggleResearch"].guiActiveEditor = isVisible;
        }

        #region Helpers
        public virtual void ConsumeResources()
        {
            double resourcePerTimeTick;
            PartResourceDefinition definition;
            Vessel.ActiveResource activeResource;

            //If we're missing any then stop the research.
            foreach (ResearchResource input in inputResources)
            {
                definition = ResourceHelper.DefinitionForResource(input.name);
                if (definition == null)
                    continue;

                //make sure the ship has enough of the resource
                activeResource = this.part.vessel.GetActiveResource(definition);
                if (activeResource == null)
                    continue;

                resourcePerTimeTick = input.amount * TimeWarp.fixedDeltaTime;
                if (activeResource.amount < resourcePerTimeTick)
                {
                    isResearching = false;
                    Events["ToggleResearch"].guiName = startResearchGUIName;
                    if (showResults)
                        ScreenMessages.PostScreenMessage(kInsufficientResources, 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    return;
                }

                //Consume the resource
                this.part.RequestResource(definition.id, resourcePerTimeTick, input.flowMode);
            }
        }

        public virtual void CalculateProgress()
        {
            //Get elapsed time (seconds)
            researchProgress = string.Format("{0:f1}%", ((elapsedTime / secondsPerCycle) * 100));
        }

        public virtual void PerformAnalysis()
        {
            float analysisRoll;
            float successBonus = 1.0f;
            float scienceGenerated;

            //Roll 3d6 to approximate a bell curve, then convert it to a value between 1 and 100.
            analysisRoll = UnityEngine.Random.Range(1, 6);
            analysisRoll += UnityEngine.Random.Range(1, 6);
            analysisRoll += UnityEngine.Random.Range(1, 6);
            analysisRoll *= 5.5556f;
            
            //Factor in crew skill
            analysisRoll += (averageCrewSkill * 10);

            //Factor in last attempt
            if (failedLastAttempt)
            {
                failedLastAttempt = false;
                analysisRoll -= kCriticalFailPenalty;
            }

            //If we failed miserably, then there's a penalty for the next attempt.
            if (analysisRoll <= kCriticalFailure)
            {
                failedLastAttempt = true;
                if (showResults)
                    ScreenMessages.PostScreenMessage(kBotchedResults, 5.0f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            else if (analysisRoll >= kCriticalSuccess)
            {
                if (showResults)
                    ScreenMessages.PostScreenMessage(kGreatResults, 5.0f, ScreenMessageStyle.UPPER_CENTER);
                successBonus = kCriticalSuccessBonus; ;
            }

            else if (analysisRoll >= kBaseSuccess)
            {
                if (showResults)
                    ScreenMessages.PostScreenMessage(kGoodResults, 5.0f, ScreenMessageStyle.UPPER_CENTER);
            }

            else
            {
                if (showResults)
                    ScreenMessages.PostScreenMessage(kNoSuccess, 5.0f, ScreenMessageStyle.UPPER_CENTER);
                successBonus = 0f;
            }

            //Add science to the resource pool
            if (successBonus > 0f)
            {
                scienceGenerated = sciencePerCycle * successBonus * (1.0f + (averageCrewSkill / 10.0f));
                ResearchAndDevelopment.Instance.AddScience(scienceGenerated, TransactionReasons.None);
            }
        }

        public double GetSecondsPerCycle()
        {
            if (averageCrewSkill == 0)
                averageCrewSkill = GetAverageSkill();
            double hoursPerCycle = Math.Pow((researchTime / (kBaseResearchDivisor + (averageCrewSkill / 10.0f))), 3.0f);

            return hoursPerCycle * 3600;
        }

        public virtual float GetAverageSkill()
        {
            float totalSkillPoints = 0f;
            int totalScientists = 0;

            if (this.part.CrewCapacity == 0)
                return 0f;

            foreach (ProtoCrewMember crewMember in this.part.protoModuleCrew)
            {
                if (crewMember.experienceTrait.TypeName == "Scientist")
                {
                    totalSkillPoints += crewMember.experienceTrait.CrewMemberExperienceLevel();
                    totalScientists += 1;
                }
            }

            return totalSkillPoints / totalScientists;
        }

        public virtual void LoadValuesFromNode(ConfigNode node)
        {
            string flowMode;
            inputResources.Clear();
            ResearchResource inputResource;
            ConfigNode[] nodeInputs = node.GetNodes("INPUT_RESOURCE");
            foreach (ConfigNode nodeInput in nodeInputs)
            {
                inputResource = new ResearchResource();
                inputResource.name = nodeInput.GetValue("name");
                inputResource.amount = float.Parse(nodeInput.GetValue("amount"));

                flowMode = nodeInput.GetValue("flowMode");
                if (string.IsNullOrEmpty(flowMode) == false)
                    inputResource.flowMode = (ResourceFlowMode)Enum.Parse(typeof(ResourceFlowMode), flowMode);
                else
                    inputResource.flowMode = ResourceFlowMode.ALL_VESSEL;

                inputResources.Add(inputResource);
            }
        }
        #endregion
    }
}
