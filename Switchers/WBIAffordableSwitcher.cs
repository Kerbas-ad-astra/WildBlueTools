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
    public class WBIAffordableSwitcher : WBIModuleSwitcher
    {
        private const string kInsufficientParts = "Insufficient resources to reconfigure the module. You need a total of {0:f2} {1:s} to reconfigure.";
        private const string kInsufficientSkill = "Insufficient skill to reconfigure the module.";
        private const string kInsufficientCrew = "Cannot reconfigure. Either crew the module or perform an EVA.";

        //Should the player pay to reconfigure the module?
        public static bool payForReconfigure = true;

        //Should we check for the required skill to redecorate?
        public static bool checkForSkill = true;

        protected float recycleBase = 0.7f;
        protected float baseSkillModifier = 0.05f;
        protected float reconfigureCost;
        protected float reconfigureCostModifier;
        protected string requriredResource;

        protected override bool payPartsCost()
        {
            if (HighLogic.LoadedSceneIsFlight == false)
                return true;
            if (!payForReconfigure)
                return true;
            PartResourceDefinition definition = ResourceHelper.DefinitionForResource("RocketParts");
            double partsPaid = this.part.RequestResource(definition.id, reconfigureCost, ResourceFlowMode.ALL_VESSEL);

            //Could we afford it?
            if (Math.Abs(partsPaid) / Math.Abs(reconfigureCost) < 0.999f)
            {
                //Put back what we took
                this.part.RequestResource(definition.id, -partsPaid, ResourceFlowMode.ALL_VESSEL);
                return false;
            }

            return true;
        }

        protected override bool canAffordReconfigure(string templateName)
        {
            if (HighLogic.LoadedSceneIsFlight == false)
                return true;
            if (!payForReconfigure)
                return true;
            string value;
            bool canAffordCost = false;

            requriredResource = templatesModel[templateName].GetValue("rocketParts");
            if (string.IsNullOrEmpty(requriredResource) == false)
            {
                float reconfigureAmount = float.Parse(requriredResource);
                PartResourceDefinition definition = ResourceHelper.DefinitionForResource(requriredResource);
                Vessel.ActiveResource resource = this.part.vessel.GetActiveResource(definition);

                //An inflatable part that hasn't been inflated yet is an automatic pass.
                if (isInflatable && !isDeployed)
                    return true;

                //Get the current template's rocket part cost.
                value = CurrentTemplate.GetValue(requriredResource);
                if (string.IsNullOrEmpty(value) == false)
                {
                    float recycleAmount = float.Parse(value);

                    //calculate the amount of parts that we can recycle.
                    recycleAmount *= calculateRecycleAmount();

                    //Now recalculate rocketPartCost, accounting for the parts we can recycle.
                    //A negative value means we'll get parts back, a positive number means we pay additional parts.
                    //Ex: current configuration takes 1200 parts. new configuration takes 900.
                    //We recycle 90% of the current configuration (1080 parts).
                    //The reconfigure cost is: 900 - 1080 = -180 parts
                    //If we reverse the numbers so new configuration takes 1200: 1200 - (900 * .9) = 390
                    reconfigureCost = reconfigureAmount - recycleAmount;
                }

                //now check to make sure the vessel has enough parts.
                if (resource == null)
                    canAffordCost =  false;

                else if (resource.amount < reconfigureCost)
                    canAffordCost =  false;
            }

            if (!canAffordCost)
            {
                string notEnoughPartsMsg = string.Format(kInsufficientParts, reconfigureCost, requriredResource);
                ScreenMessages.PostScreenMessage(notEnoughPartsMsg, 5.0f, ScreenMessageStyle.UPPER_CENTER);
                return false;
            }

            return true;
        }

        protected override bool hasSufficientSkill(string templateName)
        {
            if (HighLogic.LoadedSceneIsFlight == false)
                return true;
            if (!checkForSkill)
                return true;
            string skillRequired = templatesModel[templateName].GetValue("reconfigureSkill");
            if (string.IsNullOrEmpty(skillRequired))
                return true;
            bool hasAtLeastOneCrew = false;

            //Tearing down the current configuration returns 70% of the current configuration's rocketParts, plus 5% per skill point
            //of the highest ranking kerbal in the module with the appropriate skill required to reconfigure, or 5% per skill point
            //of the kerbal on EVA if the kerbal has the required skill.
            //If anybody can reconfigure the module to the desired template, then get the highest ranking Engineer and apply his/her skill bonus.
            if (string.IsNullOrEmpty(skillRequired))
            {
                calculateRemodelCostModifier();
                return true;
            }

            //Make sure we have an experienced person either out on EVA performing the reconfiguration, or inside the module.
            //Check EVA first
            if (FlightGlobals.ActiveVessel.isEVA)
            {
                Vessel vessel = FlightGlobals.ActiveVessel;
                Experience.ExperienceTrait experience = vessel.GetVesselCrew()[0].experienceTrait;

                if (experience.TypeName != skillRequired)
                {
                    ScreenMessages.PostScreenMessage(kInsufficientSkill, 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    return false;
                }

                calculateRemodelCostModifier(skillRequired);
                return true;
            }

            //Now check the part itself
            if (this.part.CrewCapacity == 0)
            {
                ScreenMessages.PostScreenMessage(kInsufficientCrew, 5.0f, ScreenMessageStyle.UPPER_CENTER);
                return false;
            }

            foreach (ProtoCrewMember protoCrew in this.part.protoModuleCrew)
            {
                if (protoCrew.experienceTrait.TypeName == skillRequired)
                {
                    hasAtLeastOneCrew = true;
                    break;
                }
            }

            if (!hasAtLeastOneCrew)
            {
                ScreenMessages.PostScreenMessage(kInsufficientSkill, 5.0f, ScreenMessageStyle.UPPER_CENTER);
                return false;
            }

            //Yup, we have sufficient skill.
            calculateRemodelCostModifier(skillRequired);
            return true;
        }

        protected void calculateRemodelCostModifier(string skillRequired = "Engineer")
        {
            int highestLevel = 0;

            //Check for a kerbal on EVA
            if (FlightGlobals.ActiveVessel.isEVA)
            {
                Vessel vessel = FlightGlobals.ActiveVessel;
                Experience.ExperienceTrait experience = vessel.GetVesselCrew()[0].experienceTrait;

                if (experience.TypeName == skillRequired)
                {
                    reconfigureCostModifier = baseSkillModifier * experience.CrewMemberExperienceLevel();
                    return;
                }
            }

            //No kerbal on EVA. Check the part for the highest ranking kerbal onboard with the required skill.
            if (this.part.CrewCapacity > 0)
            {
                foreach (ProtoCrewMember protoCrew in this.part.protoModuleCrew)
                {
                    if (protoCrew.experienceTrait.TypeName == skillRequired)
                        if (protoCrew.experienceLevel > highestLevel)
                            highestLevel = protoCrew.experienceLevel;
                }
            }

            reconfigureCostModifier = baseSkillModifier * highestLevel;
        }

        protected float calculateRecycleAmount()
        {
            calculateRemodelCostModifier();

            return recycleBase + reconfigureCostModifier;
        }
    }
}
