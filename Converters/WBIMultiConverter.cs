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
    public interface ITemplateOps
    {
        void DrawOpsWindow();
    }

    public class WBIMultiConverter : WBIAffordableSwitcher
    {
        //Helper objects
        protected ITemplateOps templateOps;
        protected MultiConverterModel _multiConverter;
        protected OpsView moduleOpsView;

        #region User Events & API
        public Texture GetModuleLogo(string templateName)
        {
            Texture moduleLogo = null;
            string panelName;
            ConfigNode nodeTemplate = templatesModel[templateName];

            panelName = nodeTemplate.GetValue("logoPanel");
            if (panelName != null)
                moduleLogo = GameDatabase.Instance.GetTexture(panelName, false);

            return moduleLogo;
        }

        public string GetModuleInfo(string templateName)
        {
            StringBuilder moduleInfo = new StringBuilder();
            StringBuilder converterInfo = new StringBuilder();
            ConfigNode nodeTemplate = templatesModel[templateName];
            string value;
            PartModule partModule;
            bool addConverterHeader = true;
            bool includeModuleInfo = false;

            if (nodeTemplate.HasValue("includeModuleInfo"))
                includeModuleInfo = bool.Parse(nodeTemplate.GetValue("includeModuleInfo"));

            value = nodeTemplate.GetValue("title");
            if (!string.IsNullOrEmpty(value))
                moduleInfo.Append(value + "\r\n");

            value = nodeTemplate.GetValue("description");
            if (!string.IsNullOrEmpty(value))
                moduleInfo.Append("\r\n" + value + "\r\n");

            value = nodeTemplate.GetValue("CrewCapacity");
            if (!string.IsNullOrEmpty(value))
                moduleInfo.Append("Crew Capacity: " + nodeTemplate.GetValue("CrewCapacity") + "\r\n");

            //Add just the converters
            foreach (ConfigNode moduleNode in nodeTemplate.nodes)
            {
                if (moduleNode.GetValue("name") == "ModuleResourceConverter")
                {
                    if (addConverterHeader)
                    {
                        converterInfo.Append("\r\n<b>Conversions</b>\r\n\r\n");
                        addConverterHeader = false;
                    }

                    partModule = this.part.AddModule("ModuleResourceConverter");
                    partModule.Load(moduleNode);
                    converterInfo.Append(partModule.GetInfo());
                    converterInfo.Append("\r\n");
                    this.part.RemoveModule(partModule);
                }

                else if (includeModuleInfo)
                {
                    partModule = this.part.AddModule(moduleNode.GetValue("name"));
                    partModule.Load(moduleNode);
                    moduleInfo.Append(partModule.GetInfo());
                    moduleInfo.Append("\r\n");
                    this.part.RemoveModule(partModule);
                }
            }

            moduleInfo.Append(converterInfo.ToString());

            return moduleInfo.ToString();
        }

        public void PreviewNextTemplate(string templateName)
        {
            //Get the template index associated with the template name
            int curTemplateIndex = templatesModel.FindIndexOfTemplate(templateName);

            //Get the next available template index
            int templateIndex = templatesModel.GetNextUsableIndex(curTemplateIndex);

            //Set preview name to the new template's name
            moduleOpsView.previewName = templatesModel[templateIndex].GetValue("shortName");
            moduleOpsView.cost = templatesModel[templateIndex].GetValue("rocketParts");

            //Get next template name
            templateIndex = templatesModel.GetNextUsableIndex(templateIndex);
            if (templateIndex != -1 && templateIndex != curTemplateIndex)
                moduleOpsView.nextName = templatesModel[templateIndex].GetValue("shortName");

            //Get previous template name
            moduleOpsView.prevName = templateName;
        }

        public void PreviewPrevTemplate(string templateName)
        {
            //Get the template index associated with the template name
            int curTemplateIndex = templatesModel.FindIndexOfTemplate(templateName);

            //Get the previous available template index
            int templateIndex = templatesModel.GetPrevUsableIndex(curTemplateIndex);

            //Set preview name to the new template's name
            moduleOpsView.previewName = templatesModel[templateIndex].GetValue("shortName");
            moduleOpsView.cost = templatesModel[templateIndex].GetValue("rocketParts");

            //Get next template name (which will be the current template)
            moduleOpsView.nextName = templateName;

            //Get previous template name
            templateIndex = templatesModel.GetPrevUsableIndex(templateIndex);
            if (templateIndex != -1 && templateIndex != curTemplateIndex)
                moduleOpsView.prevName = templatesModel[templateIndex].GetValue("shortName");
        }

        public void SwitchTemplateType(string templateName)
        {
            Log("SwitchTemplateType called.");

            //Can we use the index?
            EInvalidTemplateReasons reasonCode = templatesModel.CanUseTemplate(templateName);
            if (reasonCode == EInvalidTemplateReasons.TemplateIsValid)
            {
                //If we require specific skills to perform the reconfigure, do we have sufficient skill to reconfigure it?
                if (checkForSkill)
                {
                    if (hasSufficientSkill(templateName) == false)
                        return;
                }

                //If we have to pay to reconfigure the module, then do our checks.
                if (payForReconfigure)
                {
                    //Can we afford it?
                    if (canAffordReconfigure(templateName) == false)
                        return;

                    //Yup, we can afford it
                    //Pay the reconfigure cost
                    payPartsCost();
                }

                //Update contents
                UpdateContentsAndGui(templateName);
                return;
            }

            switch (reasonCode)
            {
                case EInvalidTemplateReasons.InvalidIndex:
                    ScreenMessages.PostScreenMessage("Cannot find a suitable template.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    break;

                case EInvalidTemplateReasons.TechNotUnlocked:
                    ScreenMessages.PostScreenMessage("More research is required to switch to the module.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    break;

                default:
                    ScreenMessages.PostScreenMessage("Could not switch the module.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    break;
            }
        }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Manage Operations", active = true, guiActiveUnfocused = true, unfocusedRange = 3.0f)]
        public void ManageOperations()
        {
            Log("ManageOperations called");
            int templateIndex = CurrentTemplateIndex;
            bool hasRequiredTechToReconfigure = true;

            //Set short name
            moduleOpsView.shortName = shortName;

            //Minimum tech
            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER && string.IsNullOrEmpty(techRequiredToReconfigure) == false)
                hasRequiredTechToReconfigure = ResearchAndDevelopment.GetTechnologyState(techRequiredToReconfigure) == RDTech.State.Available ? true : false;
            moduleOpsView.techResearched = fieldReconfigurable & hasRequiredTechToReconfigure;

            //Set preview, next, and previous
            if (HighLogic.LoadedSceneIsEditor == false)
            {
                moduleOpsView.previewName = shortName;
                moduleOpsView.cost = templatesModel[templateIndex].GetValue("rocketParts");

                templateIndex = templatesModel.GetNextUsableIndex(CurrentTemplateIndex);
                if (templateIndex != -1 && templateIndex != CurrentTemplateIndex)
                    moduleOpsView.nextName = templatesModel[templateIndex].GetValue("shortName");

                templateIndex = templatesModel.GetPrevUsableIndex(CurrentTemplateIndex);
                if (templateIndex != -1 && templateIndex != CurrentTemplateIndex)
                    moduleOpsView.prevName = templatesModel[templateIndex].GetValue("shortName");
            }

            moduleOpsView.ToggleVisible();
        }
        #endregion

        #region Module Overrides

        public override void OnUpdate()
        {
            base.OnUpdate();

            //Show/hide the inflate/deflate button depending upon whether or not crew is aboard
            if (this.part.protoModuleCrew.Count() > 0)
            {
                Events["ToggleInflation"].guiActive = false;
                Events["ToggleInflation"].guiActiveUnfocused = false;
            }

            else
            {
                Events["ToggleInflation"].guiActive = true;
                Events["ToggleInflation"].guiActiveUnfocused = true;
            }
        }

        public override void ToggleInflation()
        {
            PartResourceDefinition definition = ResourceHelper.DefinitionForResource("RocketParts");
            Vessel.ActiveResource resource = this.part.vessel.GetActiveResource(definition);
            string parts = CurrentTemplate.GetValue("rocketParts");

            if (string.IsNullOrEmpty(parts))
            {
                base.ToggleInflation();
                return;
            }
            float partCost = float.Parse(parts);

            calculateRemodelCostModifier();
            float adjustedPartCost = partCost;
            if (reconfigureCostModifier > 0f)
                adjustedPartCost *= reconfigureCostModifier;

            Log("FRED payForReconfigure: " + payForReconfigure);
            //Do we pay for resources? If so, either pay the resources if we're deploying the module, or refund the recycled parts
            if (payForReconfigure)
            {
                //If we aren't deployed then see if we can afford to pay the resource cost.
                if (!isDeployed)
                {
                    //Can we afford it?
                    if (resource == null || resource.amount < adjustedPartCost)
                    {
                        notEnoughParts();
                        string notEnoughPartsMsg = string.Format("Insufficient resources to assemble the module. You need a total of {0:f2} RocketParts to assemble.", partCost);
                        ScreenMessages.PostScreenMessage(notEnoughPartsMsg, 5.0f, ScreenMessageStyle.UPPER_CENTER);
                        return;
                    }

                    //Yup, we can afford it
                    //Pay the reconfigure cost
                    reconfigureCost = adjustedPartCost;
                    payPartsCost();

                    // Toggle after payment.
                    base.ToggleInflation();
                }

                //We are deployed, calculate the amount of parts that can be refunded.
                else
                {
                    // Toggle first in case deflate confirmation is needed, we'll check the state after the toggle.
                    base.ToggleInflation();

                    // deflateConfirmed's logic seems backward.
                    if (!HasResources() || (HasResources() && deflateConfirmed == false))
                    {
                        // The part came from the factory configured which represents an additional resource cost. If reconfigured in the field, the difference was paid at
                        // that time. Deflating doesn't remove any functionality, so no refund beyond the original adjusted part cost.
                        float recycleAmount = adjustedPartCost;

                        //Do we have sufficient space in the vessel to store the recycled parts?
                        float availableStorage = (float)(resource.maxAmount - resource.amount);

                        if (availableStorage < recycleAmount)
                        {
                            float amountLost = recycleAmount - availableStorage;
                            ScreenMessages.PostScreenMessage(string.Format("Module deflated, {0:f2} {1:s} lost due to insufficient storage.", amountLost, "RocketParts"), 5.0f, ScreenMessageStyle.UPPER_CENTER);

                            //We'll only recycle what we have room to store.
                            recycleAmount = availableStorage;
                        }

                        //Yup, we have the space
                        reconfigureCost = -recycleAmount;
                        payPartsCost();
                    }
                }
            }

            // Not paying for reconfiguration, check for skill requirements
            else
            {
                if (checkForSkill)
                {
                    if (hasSufficientSkill(CurrentTemplateName))
                        base.ToggleInflation();
                    else
                        return;
                }

                else
                {
                    base.ToggleInflation();
                }
            }
        }

        protected virtual void notEnoughParts()
        {
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            //Create the multiConverter
            _multiConverter = new MultiConverterModel(this.part, this.vessel, new LogDelegate(Log));

            //Tell multiConverter to store converter status.
            _multiConverter.Load(node);
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            if (_multiConverter != null)
                _multiConverter.Save(node);
        }

        public override void OnActive()
        {
            base.OnActive();
        }

        public override void OnStart(PartModule.StartState state)
        {
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)
                return;

            //Create the multiConverter. We have to do this when we're in the VAB/SPH.
            if (_multiConverter == null)
                _multiConverter = new MultiConverterModel(this.part, this.vessel, new LogDelegate(Log));

            //Create the module ops window.
            createModuleOpsView();

            //Now we can call the base method.
            base.OnStart(state);

            //Start the multiConverter
            _multiConverter.OnStart(state);

            //Fix module indexes (for things like the science lab)
            fixModuleIndexes();
        }

        #endregion

        #region Helpers
        protected virtual void drawTemplateOps()
        {
            if (templateOps != null)
                templateOps.DrawOpsWindow();
        }

        protected virtual bool templateHasOpsWindow()
        {
            templateOps = this.part.FindModuleImplementing<ITemplateOps>();

            if (templateOps != null)
                return true;
            else
                return false;
        }

        public void OnGUI()
        {
            if (moduleOpsView != null)
                moduleOpsView.OnGUI();
        }

        public override void OnRedecorateModule(ConfigNode templateNode, bool payForRedecoration)
        {
            base.OnRedecorateModule(templateNode, payForRedecoration);

            //Play a nice construction sound effect

            //Next, create converters as specified in the template and set their values.
            _multiConverter.LoadConvertersFromTemplate(templateNode);
        }

         public override void UpdateContentsAndGui(int templateIndex)
        {
            base.UpdateContentsAndGui(templateIndex);
            string templateName;

            //Change the OpsView's names
            moduleOpsView.shortName = shortName;

            templateIndex = templatesModel.GetNextUsableIndex(CurrentTemplateIndex);
            if (templateIndex != -1 && templateIndex != CurrentTemplateIndex)
            {
                templateName = templatesModel[templateIndex].GetValue("shortName");
                moduleOpsView.nextName = templateName;
            }

            else
            {
                moduleOpsView.nextName = "none available";
            }

            templateIndex = templatesModel.GetPrevUsableIndex(CurrentTemplateIndex);
            if (templateIndex != -1 && templateIndex != CurrentTemplateIndex)
            {
                templateName = templatesModel[templateIndex].GetValue("shortName");
                moduleOpsView.prevName = templateName;
            }

            else
            {
                moduleOpsView.prevName = "none available";
            }

            if (moduleOpsView.IsVisible())
            {
                moduleOpsView.converters = _multiConverter.converters;
                moduleOpsView.resources = this.part.Resources;
            }
        }

        protected virtual void createModuleOpsView()
        {
            Log("createModuleOpsView called");

            moduleOpsView = new OpsView();
            moduleOpsView.converters = _multiConverter.converters;
            moduleOpsView.part = this.part;
            moduleOpsView.resources = this.part.Resources;
            moduleOpsView.nextModuleDelegate = new NextModule(NextType);
            moduleOpsView.prevModuleDelegate = new PrevModule(PrevType);
            moduleOpsView.nextPreviewDelegate = new NextPreviewModule(PreviewNextTemplate);
            moduleOpsView.prevPreviewDelegate = new PrevPreviewModule(PreviewPrevTemplate);
            moduleOpsView.getModuleInfoDelegate = new GetModuleInfo(GetModuleInfo);
            moduleOpsView.changeModuleTypeDelegate = new ChangeModuleType(SwitchTemplateType);
            moduleOpsView.getModuleLogoDelegate = new GetModuleLogo(GetModuleLogo);
            moduleOpsView.teplateHasOpsWindowDelegate = new TemplateHasOpsWindow(templateHasOpsWindow);
            moduleOpsView.drawTemplateOpsDelegate = new DrawTemplateOps(drawTemplateOps);
            moduleOpsView.GetPartModules();
        }

        protected override void hideEditorGUI(PartModule.StartState state)
        {
            base.hideEditorGUI(state);
        }

        protected override void initModuleGUI()
        {
            base.initModuleGUI();
            int index;
            string value;

            //Change the toggle button's name
            index = templatesModel.GetNextUsableIndex(CurrentTemplateIndex);
            if (index != -1 && index != CurrentTemplateIndex)
            {
                value = templatesModel.templateNodes[index].GetValue("shortName");
                moduleOpsView.nextName = value;
            }

            index = templatesModel.GetPrevUsableIndex(CurrentTemplateIndex);
            if (index != -1 && index != CurrentTemplateIndex)
            {
                value = templatesModel.templateNodes[index].GetValue("shortName");
                moduleOpsView.prevName = value;
            }
        }
        #endregion

    }
}