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
    public class WBIBasicScienceLab : WBIResourceConverter
    {
        private const float kBaseResearchDivisor = 1.75f;
        private const float kDefaultResearchTime = 7f;
        private const float kCriticalSuccessBonus = 1.5f;
        private const string kResearchCriticalFail = "Botched Results";
        private const string kResearchFail = "Inconclusive";
        private const string kResearchCriticalSuccess = "Great Results";
        private const string kResearchSuccess = "Good Results";

        protected float kMessageDuration = 6.5f;
        protected string botchedResultsMsg = "Botched results! This may have consequences in the future...";
        protected string greatResultsMsg = "Analysis better than expected!";
        protected string goodResultsMsg = "Good results!";
        protected string noSuccessMsg = "Analysis inconclusive";
        protected string scienceAddedMsg = "<color=lightblue>Science added: {0:f2}</b></color>";
        protected string reputationAddedMsg = "<color=yellow>Reputation added: {0:f2}</b></color>";
        protected string fundsAddedMsg = "<color=lime>Funds added: {0:f2}</b></color>";
        protected string noResearchDataMsg = "No data to transmit yet, check back later.";
        protected string researchingMsg = "Researching";
        protected string readyMsg = "Ready";

        [KSPField]
        public float sciencePerCycle;

        [KSPField]
        public float reputationPerCycle;

        [KSPField]
        public float fundsPerCycle;

        [KSPField(isPersistant = true)]
        public float scienceAdded;

        [KSPField(isPersistant = true)]
        public float reputationAdded;

        [KSPField(isPersistant = true)]
        public float fundsAdded;

        protected bool failedLastAttempt;
        protected float successBonus;
        protected string experimentID;
        protected float dataAmount;
        protected FakeExperimentResults fakeExperiment;
        protected TransmitHelper transmitHelper = new TransmitHelper();

        #region Actions And Events
        [KSPEvent(guiActive = true, guiActiveUnfocused = true, unfocusedRange = 3.0f, guiName = "Review Data")]
        public virtual void ReviewData()
        {
            if (scienceAdded < 0.001 && reputationAdded < 0.001 && fundsAdded < 0.001)
            {
                ScreenMessages.PostScreenMessage(noResearchDataMsg, 5.0f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            ModuleScienceLab lab = null;
            List<ModuleScienceLab> labs = this.part.vessel.FindPartModulesImplementing<ModuleScienceLab>();
            if (labs != null)
                if (labs.Count > 0)
                    lab = labs.First<ModuleScienceLab>();

            fakeExperiment.ShowResults(experimentID, dataAmount, lab);
        }

        #endregion

        #region Overrides

        public override string GetInfo()
        {
            StringBuilder moduleInfo = new StringBuilder();
            moduleInfo.Append(base.GetInfo() + "\r\n\r\n");

            moduleInfo.Append(string.Format("Research Time: {0:f2}hrs\r\n", GetSecondsPerCycle() / 3600f));

            if (sciencePerCycle > 0f)
                moduleInfo.Append(string.Format("Science Per Cycle: {0:f2}\r\n", sciencePerCycle));

            if (reputationPerCycle > 0f)
                moduleInfo.Append(string.Format("Reputation Per Cycle: {0:f2}\r\n", reputationPerCycle));

            if (fundsPerCycle > 0f)
                moduleInfo.Append(string.Format("Funds Per Cycle: {0:f2}\r\n", fundsPerCycle));

            return moduleInfo.ToString();
        }

        public override void OnStart(StartState state)
        {
            UnityEngine.Random.seed = (int)System.DateTime.Now.Ticks;
            base.OnStart(state);
            if (HighLogic.LoadedSceneIsFlight == false)
                return;

            //Setup
            fakeExperiment = new FakeExperimentResults();
            fakeExperiment.part = this.part;
            fakeExperiment.transmitDelegate = transmitResults;
            transmitHelper.part = this.part;

            attemptCriticalFail = kResearchCriticalFail;
            attemptCriticalSuccess = kResearchCriticalSuccess;
            attemptFail = kResearchFail;
            attemptSuccess = kResearchSuccess;
        }

        #endregion

        #region Helpers
        public override double GetSecondsPerCycle()
        {
            if (totalCrewSkill == 0)
                totalCrewSkill = GetTotalCrewSkill();
            double researchTime = Math.Pow((hoursPerCycle / (kBaseResearchDivisor + (totalCrewSkill / 10.0f))), 3.0f);

            return researchTime * 3600;
        }

        protected virtual void transmitResults(ScienceData data)
        {
            if (transmitHelper.TransmitToKSC(scienceAdded, reputationAdded, fundsAdded))
            {
                scienceAdded = 0f;
                reputationAdded = 0f;
                fundsAdded = 0f;
            }
        }

        protected override void onCriticalFailure()
        {
            base.onCriticalFailure();
        }

        protected override void onCriticalSuccess()
        {
            base.onCriticalSuccess();
            successBonus = kCriticalSuccessBonus;
            addCurrency();
        }

        protected override void onFailure()
        {
            base.onFailure();
        }

        protected override void onSuccess()
        {
            base.onSuccess();
            successBonus = 1.0f;
            addCurrency();
        }

        protected virtual void addCurrency()
        {
            float successFactor = successBonus * (1.0f + (totalCrewSkill / 10.0f));

            //Add science to the resource pool
            if (sciencePerCycle > 0.0f)
                scienceAdded += sciencePerCycle * successFactor;

            //Reputation
            if (reputationPerCycle > 0.0f)
                reputationAdded += reputationPerCycle * successFactor;

            //Funds
            if (fundsPerCycle > 0.0f)
                fundsAdded += fundsPerCycle * successFactor;
        }
        #endregion
    }
}
