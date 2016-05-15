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
    public class WBIConvertibleStorage : WBIAffordableSwitcher
    {
        ConvertibleStorageView storageView = new ConvertibleStorageView();

        [KSPEvent(guiActive = true, guiActiveUnfocused = true, unfocusedRange = 5.0f, guiName = "Reconfigure Storage")]
        public void ReconfigureStorage()
        {
            setupStorageView(CurrentTemplateIndex);

            storageView.ToggleVisible();
        }

        public override void SetGUIVisible(bool isVisible)
        {
            base.SetGUIVisible(isVisible);

            if (HighLogic.LoadedSceneIsFlight)
            {
                this.Events["NextType"].guiActive = false;
                this.Events["PrevType"].guiActive = false;
            }

            Events["ReconfigureStorage"].guiActive = isVisible;
            Events["ReconfigureStorage"].guiActiveUnfocused = isVisible;
        }

        protected override void initModuleGUI()
        {
            base.initModuleGUI();

            hideEditorButtons();

            storageView.previewNext = PreviewNext;
            storageView.previewPrev = PreviewPrev;
            storageView.setTemplate = SwitchTemplateType;
        }

        public override void RedecorateModule(bool loadTemplateResources = true)
        {
            base.RedecorateModule(loadTemplateResources);

            hideEditorButtons();
        }

        protected void hideEditorButtons()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                Events["NextType"].guiActive = false;
                Events["NextType"].guiActiveUnfocused = false;
                Events["NextType"].guiActive = false;

                Events["PrevType"].guiActive = false;
                Events["PrevType"].guiActiveUnfocused = false;
                Events["PrevType"].guiActive = false;
            }
        }

        public void PreviewNext(string templateName)
        {
            //Get the template index associated with the template name
            int curTemplateIndex = templateManager.FindIndexOfTemplate(templateName);

            //Get the next available template index
            int templateIndex = templateManager.GetNextUsableIndex(curTemplateIndex);

            setupStorageView(templateIndex);
        }

        public void PreviewPrev(string templateName)
        {
            //Get the template index associated with the template name
            int curTemplateIndex = templateManager.FindIndexOfTemplate(templateName);

            //Get the previous available template index
            int templateIndex = templateManager.GetPrevUsableIndex(curTemplateIndex);

            setupStorageView(templateIndex);
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

        protected void setupStorageView(int templateIndex)
        {
            //Template count
            storageView.templateCount = templateManager.templateNodes.Length;

            //Template name
            storageView.templateName = templateManager[templateIndex].GetValue("shortName");

            //Required resource
            if (templateManager[templateIndex].HasValue("requiredResource"))
                storageView.requiredResource = templateManager[templateIndex].GetValue("requiredResource");

            //Resource cost
            if (templateManager[templateIndex].HasValue("requiredAmount"))
                storageView.resourceCost = float.Parse(templateManager[templateIndex].GetValue("requiredAmount"));
            else
                storageView.resourceCost = 0f;

            //Required skill
            if (templateManager[templateIndex].HasValue("reconfigureSkill"))
                storageView.requiredSkill = templateManager[templateIndex].GetValue("reconfigureSkill");
            else
                storageView.requiredSkill = string.Empty;

            //Description
            storageView.info = getStorageInfo(templateIndex);

            //Decal
            string panelName;
            ConfigNode nodeTemplate = templateManager[templateIndex];

            panelName = nodeTemplate.GetValue("logoPanel");
            if (panelName != null)
                storageView.decal = GameDatabase.Instance.GetTexture(panelName, false);
            else
                storageView.decal = null;
        }

        protected string getStorageInfo(int templateIndex)
        {
            StringBuilder moduleInfo = new StringBuilder();
            StringBuilder converterInfo = new StringBuilder();
            ConfigNode nodeTemplate = templateManager[templateIndex];
            string value;
            PartModule partModule;
            bool addConverterHeader = true;
            double maxAmount = 0f;

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

                else
                {
                    partModule = this.part.AddModule(moduleNode.GetValue("name"));
                    partModule.Load(moduleNode);
                    moduleInfo.Append(partModule.GetInfo());
                    moduleInfo.Append("\r\n");
                    this.part.RemoveModule(partModule);
                }
            }

            moduleInfo.Append(converterInfo.ToString());

            //Resources
            ConfigNode[] resources = nodeTemplate.GetNodes("RESOURCE");
            if (resources.Length > 0)
            {
                if (isInflatable)
                    moduleInfo.Append("\r\n<b>Resources (deployed)</b>\r\n\r\n");
                else
                    moduleInfo.Append("\r\n<b>Resources</b>\r\n\r\n");

                foreach (ConfigNode resourceNode in resources)
                {
                    maxAmount = double.Parse(resourceNode.GetValue("maxAmount")) * capacityFactor;

                    moduleInfo.Append(string.Format("{0:s}: {1:f2}\r\n", resourceNode.GetValue("name"), maxAmount));
                }
            }

            return moduleInfo.ToString();
        }

        public void OnGUI()
        {
            if (storageView.IsVisible())
                storageView.DrawWindow();
        }

    }
}
