using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;

/*
Source code copyright 2016, by Michael Billard (Angel-125)
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

    public interface ITemplateOps2 : ITemplateOps
    {
        void SetOpsView(OpsView view);
    }

    public class WBIMultiConverter : WBIAffordableSwitcher
    {
        [KSPField]
        public float productivity = 1.0f;

        [KSPField]
        public float efficiency = 1.0f;

        //Helper objects
        protected ITemplateOps templateOps;
        protected OpsView moduleOpsView = new OpsView();

        #region User Events & API
        public Texture GetModuleLogo(string templateName)
        {
            Texture moduleLogo = null;
            string panelName;
            ConfigNode nodeTemplate = templateManager[templateName];

            panelName = nodeTemplate.GetValue("logoPanel");
            if (panelName != null)
                moduleLogo = GameDatabase.Instance.GetTexture(panelName, false);

            return moduleLogo;
        }

        public virtual string GetModuleInfo(string templateName)
        {
            StringBuilder moduleInfo = new StringBuilder();
            StringBuilder converterInfo = new StringBuilder();
            ConfigNode nodeTemplate = templateManager[templateName];
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
            ConfigNode[] moduleNodes = nodeTemplate.nodes.GetNodes("MODULE");
            foreach (ConfigNode moduleNode in moduleNodes)
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
            int curTemplateIndex = templateManager.FindIndexOfTemplate(templateName);

            //Get the next available template index
            int templateIndex = templateManager.GetNextUsableIndex(curTemplateIndex);

            //Set preview name to the new template's name
            moduleOpsView.previewName = templateManager[templateIndex].GetValue("shortName");
            moduleOpsView.cost = getTemplateCost(templateIndex);
            moduleOpsView.requiredResource = templateManager[templateIndex].GetValue("requiredResource");

            //Get next template name
            templateIndex = templateManager.GetNextUsableIndex(templateIndex);
            if (templateIndex != -1 && templateIndex != curTemplateIndex)
                moduleOpsView.nextName = templateManager[templateIndex].GetValue("shortName");

            //Get previous template name
            moduleOpsView.prevName = templateName;
        }

        public void PreviewPrevTemplate(string templateName)
        {
            //Get the template index associated with the template name
            int curTemplateIndex = templateManager.FindIndexOfTemplate(templateName);

            //Get the previous available template index
            int templateIndex = templateManager.GetPrevUsableIndex(curTemplateIndex);

            //Set preview name to the new template's name
            moduleOpsView.previewName = templateManager[templateIndex].GetValue("shortName");
            moduleOpsView.cost = getTemplateCost(templateIndex);
            moduleOpsView.requiredResource = templateManager[templateIndex].GetValue("requiredResource");

            //Get next template name (which will be the current template)
            moduleOpsView.nextName = templateName;

            //Get previous template name
            templateIndex = templateManager.GetPrevUsableIndex(templateIndex);
            if (templateIndex != -1 && templateIndex != curTemplateIndex)
                moduleOpsView.prevName = templateManager[templateIndex].GetValue("shortName");
        }

        public void SwitchTemplateType(string templateName)
        {
            Log("SwitchTemplateType called.");

            //Can we use the index?
            EInvalidTemplateReasons reasonCode = templateManager.CanUseTemplate(templateName);
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
                    payPartsCost(templateManager.FindIndexOfTemplate(templateName));
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

            //Set short name
            moduleOpsView.shortName = shortName;

            //Minimum tech
            moduleOpsView.techResearched = true;
            moduleOpsView.fieldReconfigurable = fieldReconfigurable;

            //Templates
            moduleOpsView.templateCount = templateManager.templateNodes.Length;

            //Set preview, next, and previous
            if (HighLogic.LoadedSceneIsEditor == false)
            {
                moduleOpsView.previewName = shortName;
                moduleOpsView.cost = getTemplateCost(templateIndex);
                moduleOpsView.requiredResource = templateManager[templateIndex].GetValue("requiredResource");

                templateIndex = templateManager.GetNextUsableIndex(CurrentTemplateIndex);
                if (templateIndex != -1 && templateIndex != CurrentTemplateIndex)
                    moduleOpsView.nextName = templateManager[templateIndex].GetValue("shortName");

                templateIndex = templateManager.GetPrevUsableIndex(CurrentTemplateIndex);
                if (templateIndex != -1 && templateIndex != CurrentTemplateIndex)
                    moduleOpsView.prevName = templateManager[templateIndex].GetValue("shortName");
            }

            moduleOpsView.SetVisible(true);
        }
        #endregion

        #region Module Overrides

        public override void OnUpdate()
        {
            base.OnUpdate();

            //Show/hide the inflate/deflate button depending upon whether or not crew is aboard
            if (isInflatable)
            {
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
        }

        public override void ToggleInflation()
        {
            string requiredName = CurrentTemplate.GetValue("requiredResource");
            PartResourceDefinition definition = ResourceHelper.DefinitionForResource(requiredName);
            Vessel.ActiveResource resource = this.part.vessel.GetActiveResource(definition);
            string parts = CurrentTemplate.GetValue("requiredAmount");

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
                        string notEnoughPartsMsg = string.Format("Insufficient resources to assemble the module. You need a total of {0:f2} " + requiredName + " to assemble.", partCost);
                        ScreenMessages.PostScreenMessage(notEnoughPartsMsg, 5.0f, ScreenMessageStyle.UPPER_CENTER);
                        return;
                    }

                    //Yup, we can afford it
                    //Pay the reconfigure cost
                    reconfigureCost = adjustedPartCost;
                    payPartsCost(CurrentTemplateIndex);

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
                            ScreenMessages.PostScreenMessage(string.Format("Module deflated, {0:f2} {1:s} lost due to insufficient storage.", amountLost, requiredName), 5.0f, ScreenMessageStyle.UPPER_CENTER);

                            //We'll only recycle what we have room to store.
                            recycleAmount = availableStorage;
                        }

                        //Yup, we have the space
                        reconfigureCost = -recycleAmount;
                        payPartsCost(CurrentTemplateIndex);
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

        public override void OnStart(PartModule.StartState state)
        {
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)
                return;

            //Create the module ops window.
            createModuleOpsView();

            //Now we can call the base method.
            base.OnStart(state);

            moduleOpsView.UpdateConverters();
        }

        #endregion

        #region Helpers
        protected string getTemplateCost(int templateIndex)
        {
            if (templateManager[templateIndex].HasValue("requiredAmount"))
            {
                float cost = calculateRemodelCost(templateIndex);
                return string.Format("{0:f2}", cost);
            }
            else
                return "0";
        }

        protected virtual void drawTemplateOps()
        {
            if (templateOps != null)
                templateOps.DrawOpsWindow();
        }

        protected virtual bool templateHasOpsWindow()
        {
            ITemplateOps2 templateOps2 = this.part.FindModuleImplementing<ITemplateOps2>();
            if (templateOps2 != null)
                templateOps2.SetOpsView(moduleOpsView);

            templateOps = this.part.FindModuleImplementing<ITemplateOps>();

            if (templateOps != null)
                return true;
            else
                return false;
        }

        public void OnGUI()
        {
            try
            {
                if (moduleOpsView.IsVisible())
                    moduleOpsView.DrawWindow();
            }
            catch (Exception ex)
            {
                Debug.Log("Error in WBIMultiConverter-OnGUI: " + ex.ToString());
            }
        }

        protected override void loadModulesFromTemplate(ConfigNode templateNode)
        {
            base.loadModulesFromTemplate(templateNode);

            List<ModuleResourceConverter> converters = this.part.FindModulesImplementing<ModuleResourceConverter>();
            foreach (ModuleResourceConverter converter in converters)
            {
                if (converter is WBIBasicScienceLab == false)
                    runHeadless(converter);
            }

            moduleOpsView.UpdateConverters();
        }

        public override void UpdateContentsAndGui(int templateIndex)
        {
            base.UpdateContentsAndGui(templateIndex);
            string templateName;

            //Change the OpsView's names
            moduleOpsView.shortName = shortName;

            templateIndex = templateManager.GetNextUsableIndex(CurrentTemplateIndex);
            if (templateIndex != -1 && templateIndex != CurrentTemplateIndex)
            {
                templateName = templateManager[templateIndex].GetValue("shortName");
                moduleOpsView.nextName = templateName;
            }

            else
            {
                moduleOpsView.nextName = "none available";
            }

            templateIndex = templateManager.GetPrevUsableIndex(CurrentTemplateIndex);
            if (templateIndex != -1 && templateIndex != CurrentTemplateIndex)
            {
                templateName = templateManager[templateIndex].GetValue("shortName");
                moduleOpsView.prevName = templateName;
            }

            else
            {
                moduleOpsView.prevName = "none available";
            }

            //Update productivity and efficiency
            updateProductivity();

            if (moduleOpsView.IsVisible())
            {
                moduleOpsView.UpdateConverters();
                moduleOpsView.resources = this.part.Resources;
            }
        }

        protected virtual void updateProductivity()
        {
            //Find all the resource converters and set their productivity
            List<ModuleResourceConverter> converters = this.part.FindModulesImplementing<ModuleResourceConverter>();

            foreach (ModuleResourceConverter converter in converters)
            {
                converter.Efficiency = efficiency;

                //Now adjust the output.
                foreach (ResourceRatio ratio in converter.outputList)
                    ratio.Ratio *= productivity;
            }
        }

        protected virtual void runHeadless(ModuleResourceConverter converter)
        {
            foreach (BaseEvent baseEvent in converter.Events)
            {
                baseEvent.guiActive = false;
                baseEvent.guiActiveEditor = false;
            }

            foreach (BaseField baseField in converter.Fields)
            {
                baseField.guiActive = false;
                baseField.guiActiveEditor = false;
            }

            //Dirty the GUI
            UIPartActionWindow tweakableUI = Utils.FindActionWindow(this.part);
            if (tweakableUI != null)
                tweakableUI.displayDirty = true;
        }
        
        protected virtual void createModuleOpsView()
         {
             Log("createModuleOpsView called");

             try
             {
                 //moduleOpsView.converters = _multiConverter.converters;
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
                 moduleOpsView.UpdateConverters();
             }
             catch (Exception ex)
             {
                 Debug.Log("Exception in createModuleOpsView: " + ex.ToString());
             }
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
            bool showNextPrevButtons = HighLogic.LoadedSceneIsEditor ? true : false;

            //Next/prev buttons
            Events["NextType"].guiActive = showNextPrevButtons;
            Events["NextType"].active = showNextPrevButtons;
            Events["PrevType"].guiActive = showNextPrevButtons;
            Events["PrevType"].active = showNextPrevButtons;

            Events["ManageOperations"].active = ShowGUI;

            //Change the toggle button's name
            index = templateManager.GetNextUsableIndex(CurrentTemplateIndex);
            if (index != -1 && index != CurrentTemplateIndex)
            {
                value = templateManager.templateNodes[index].GetValue("shortName");
                moduleOpsView.nextName = value;
            }

            index = templateManager.GetPrevUsableIndex(CurrentTemplateIndex);
            if (index != -1 && index != CurrentTemplateIndex)
            {
                value = templateManager.templateNodes[index].GetValue("shortName");
                moduleOpsView.prevName = value;
            }
        }
        #endregion

    }
}