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
    public class WBIMultiConverter : WBIModuleSwitcher
    {
        //Helper objects
        private MultiConverterModel _multiConverter;
        private OpsView _moduleOpsView;

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
            ConfigNode nodeTemplate = templatesModel[templateName];
            string value;
            PartModule converter;
            bool addConverterHeader = true;

            value = nodeTemplate.GetValue("title");
            if (!string.IsNullOrEmpty(value))
                moduleInfo.Append(value + "\r\n");

            value = nodeTemplate.GetValue("description");
            if (!string.IsNullOrEmpty(value))
                moduleInfo.Append("\r\n" + value + "\r\n");

            value = nodeTemplate.GetValue("CrewCapacity");
            if (!string.IsNullOrEmpty(value))
                moduleInfo.Append("Crew Capacity: " + nodeTemplate.GetValue("CrewCapacity") + "\r\n");

            foreach (ConfigNode nodeConverter in nodeTemplate.nodes)
            {
                if (nodeConverter.GetValue("name") == "ModuleResourceConverter")
                {
                    if (addConverterHeader)
                    {
                        moduleInfo.Append("\r\n<b>Conversions</b>\r\n\r\n");
                        addConverterHeader = false;
                    }

                    converter = this.part.AddModule("ModuleResourceConverter");
                    converter.Load(nodeConverter);
                    moduleInfo.Append(converter.GetInfo());
                    moduleInfo.Append("\r\n");
                    this.part.RemoveModule(converter);
                }
            }

            return moduleInfo.ToString();
        }

        public void PreviewNextTemplate(string templateName)
        {
            //Get the template index associated with the template name
            int curTemplateIndex = templatesModel.FindIndexOfTemplate(templateName);

            //Get the next available template index
            int templateIndex = templatesModel.GetNextUsableIndex(curTemplateIndex);

            //Set preview name to the new template's name
            _moduleOpsView.previewName = templatesModel[templateIndex].GetValue("shortName");

            //Get next template name
            templateIndex = templatesModel.GetNextUsableIndex(templateIndex);
            if (templateIndex != -1 && templateIndex != curTemplateIndex)
                _moduleOpsView.nextName = templatesModel[templateIndex].GetValue("shortName");

            //Get previous template name
            _moduleOpsView.prevName = templateName;
        }

        public void PreviewPrevTemplate(string templateName)
        {
            //Get the template index associated with the template name
            int curTemplateIndex = templatesModel.FindIndexOfTemplate(templateName);

            //Get the previous available template index
            int templateIndex = templatesModel.GetPrevUsableIndex(curTemplateIndex);

            //Set preview name to the new template's name
            _moduleOpsView.previewName = templatesModel[templateIndex].GetValue("shortName");

            //Get next template name (which will be the current template)
            _moduleOpsView.nextName = templateName;

            //Get previous template name
            templateIndex = templatesModel.GetPrevUsableIndex(templateIndex);
            if (templateIndex != -1 && templateIndex != curTemplateIndex)
                _moduleOpsView.prevName = templatesModel[templateIndex].GetValue("shortName");
        }

        public void SwitchTemplateType(string templateName)
        {
            Log("SwitchTemplateType called.");
            //Can we use the index?
            EInvalidTemplateReasons reasonCode = templatesModel.CanUseTemplate(templateName);
            if (reasonCode == EInvalidTemplateReasons.TemplateIsValid)
            {
                Log("Template is valid.");
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

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Manage Operations", active = true)]
        public void ManageOperations()
        {
            Log("ManageOperations called");
            int templateIndex = CurrentTemplateIndex;
            bool hasRequiredTechToReconfigure = true;

            //Set short name
            _moduleOpsView.shortName = shortName;

            //Minimum tech
            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER && string.IsNullOrEmpty(techRequiredToReconfigure) == false)
                hasRequiredTechToReconfigure = ResearchAndDevelopment.GetTechnologyState(techRequiredToReconfigure) == RDTech.State.Available ? true : false;
            _moduleOpsView.canBeReconfigured = fieldReconfigurable & hasRequiredTechToReconfigure;

            //Set preview, next, and previous
            if (HighLogic.LoadedSceneIsEditor == false)
            {
                _moduleOpsView.previewName = shortName;

                templateIndex = templatesModel.GetNextUsableIndex(CurrentTemplateIndex);
                if (templateIndex != -1 && templateIndex != CurrentTemplateIndex)
                    _moduleOpsView.nextName = templatesModel[templateIndex].GetValue("shortName");

                templateIndex = templatesModel.GetPrevUsableIndex(CurrentTemplateIndex);
                if (templateIndex != -1 && templateIndex != CurrentTemplateIndex)
                    _moduleOpsView.prevName = templatesModel[templateIndex].GetValue("shortName");
            }

            _moduleOpsView.ToggleVisible();
        }
        #endregion

        #region Module Overrides

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            //Create the multiConverter
            _multiConverter = new MultiConverterModel(this.part, this.vessel, new LogDelegate(Log));

            //Tell multiconverter to store converter status.
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

            //Create the multiconverter. We have to do this when we're in the VAB/SPH.
            if (_multiConverter == null)
                _multiConverter = new MultiConverterModel(this.part, this.vessel, new LogDelegate(Log));

            //Create the module ops window.
            createModuleOpsView();

            //Now we can call the base method.
            base.OnStart(state);

            //Start the multiconverter
            _multiConverter.OnStart(state);

            //Fix module indexes (for things like the science lab)
            fixModuleIndexes();
        }

        #endregion

        #region Helpers
        public void OnGUI()
        {
            if (_moduleOpsView != null)
                _moduleOpsView.OnGUI();
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
            _moduleOpsView.shortName = shortName;

            templateIndex = templatesModel.GetNextUsableIndex(CurrentTemplateIndex);
            if (templateIndex != -1 && templateIndex != CurrentTemplateIndex)
            {
                templateName = templatesModel[templateIndex].GetValue("shortName");
                _moduleOpsView.nextName = templateName;
            }

            else
            {
                _moduleOpsView.nextName = "none available";
            }

            templateIndex = templatesModel.GetPrevUsableIndex(CurrentTemplateIndex);
            if (templateIndex != -1 && templateIndex != CurrentTemplateIndex)
            {
                templateName = templatesModel[templateIndex].GetValue("shortName");
                _moduleOpsView.prevName = templateName;
            }

            else
            {
                _moduleOpsView.prevName = "none available";
            }

            if (_moduleOpsView.IsVisible())
            {
                _moduleOpsView.converters = _multiConverter.converters;
                _moduleOpsView.resources = this.part.Resources;
            }
        }

        protected void createModuleOpsView()
        {
            Log("createModuleOpsView called");

            _moduleOpsView = new OpsView();
            _moduleOpsView.converters = _multiConverter.converters;
            _moduleOpsView.part = this.part;
            _moduleOpsView.resources = this.part.Resources;
            _moduleOpsView.nextModuleDelegate = new NextModule(NextType);
            _moduleOpsView.prevModuleDelegate = new PrevModule(PrevType);
            _moduleOpsView.nextPreviewDelegate = new NextPreviewModule(PreviewNextTemplate);
            _moduleOpsView.prevPreviewDelegate = new PrevPreviewModule(PreviewPrevTemplate);
            _moduleOpsView.getModuleInfoDelegate = new GetModuleInfo(GetModuleInfo);
            _moduleOpsView.changeModuleTypeDelegate = new ChangeModuleType(SwitchTemplateType);
            _moduleOpsView.getModuleLogoDelegate = new GetModuleLogo(GetModuleLogo);
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
                _moduleOpsView.nextName = value;
            }

            index = templatesModel.GetPrevUsableIndex(CurrentTemplateIndex);
            if (index != -1 && index != CurrentTemplateIndex)
            {
                value = templatesModel.templateNodes[index].GetValue("shortName");
                _moduleOpsView.prevName = value;
            }

            if (templatesModel.templateNodes.Length == 1)
            {
                Events["ManageOperations"].guiActiveUnfocused = false;
                Events["ManageOperations"].guiActiveEditor = false;
                Events["ManageOperations"].guiActive = false;
            }
        }
        #endregion

    }
}