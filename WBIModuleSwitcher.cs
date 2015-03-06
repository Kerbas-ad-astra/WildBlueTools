using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/*
Source code copyrighgt 2014, by Michael Billard (Angel-125)
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
    public class WBIModuleSwitcher : WBIResourceSwitcher
    {
        protected List<PartModule> addedPartModules = new List<PartModule>();

        private bool _showGUI = true;
        private string _ignoreTemplateModules = "None";
        bool _loadFromTemplate;

        #region API
        public bool ShowGUI
        {
            get
            {
                return _showGUI;
            }

            set
            {
                _showGUI = value;
                initModuleGUI();
            }
        }
        #endregion

        #region Overrides
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            ConfigNode[] moduleNodes = node.GetNodes("WBIMODULE");
            PartModule addedModule;

            if (moduleNodes == null)
                return;

            foreach (ConfigNode moduleNode in moduleNodes)
            {
                moduleNode.name = "MODULE";
                addedModule = this.part.AddModule(moduleNode);
                if (addedModule != null)
                    addedPartModules.Add(addedModule);
            }

            _loadFromTemplate = addedPartModules.Count<PartModule>() > 0 ? false : true;
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            ConfigNode saveNode;

            if (addedPartModules == null)
            {
                Log("addedPartModules is null");
                return;
            }

            foreach (PartModule addedModule in addedPartModules)
            {
                saveNode = ConfigNode.CreateConfigFromObject(addedModule);
                if (saveNode == null)
                {
                    Log("save node is null");
                    continue;
                }
                saveNode.name = "WBIMODULE";
                addedModule.Save(saveNode);
                node.AddNode(saveNode);
            }
        }

        public override void OnStart(PartModule.StartState state)
        {
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)
                return;
            base.OnStart(state);
        }

        public override void OnRedecorateModule(ConfigNode templateNode, bool payForRedecoration)
        {
            Log("OnRedecorateModule called");

            //Load the modules
            if (_loadFromTemplate || HighLogic.LoadedSceneIsEditor)
                loadModulesFromTemplate(templateNode);
        }

        protected override void getProtoNodeValues(ConfigNode protoNode)
        {
            base.getProtoNodeValues(protoNode);
            string value;

            value = protoNode.GetValue("showGUI");
            if (string.IsNullOrEmpty(value) == false)
                _showGUI = bool.Parse(value);

            value = protoNode.GetValue("ignoreTemplateModules");
            if (string.IsNullOrEmpty(value) == false)
                _ignoreTemplateModules = value;
        }

        protected override void initModuleGUI()
        {
            base.initModuleGUI();
            bool showNextPrevButtons = HighLogic.LoadedSceneIsEditor ? true : false;

            //Next/prev buttons
            Events["NextType"].guiActive = showNextPrevButtons;
            Events["NextType"].active = showNextPrevButtons;
            Events["PrevType"].guiActive = showNextPrevButtons;
            Events["PrevType"].active = showNextPrevButtons;
        }

        #endregion

        #region Helpers
        protected void fixModuleIndexes()
        {
            PartModule module;
            int containerIndex = -1;
            ModuleScienceLab sciLab;

                /*
                 * Special case: ModuleScienceLab
                 * ModuleScienceLab has a field called "containerModuleIndex"
                 * which is the index into the part's array of PartModule objects.
                 * When you specify a number like, say 0, then the MobileScienceLab
                 * expects that the very first PartModule in the array of part.Modules
                 * will be a ModuleScienceContainer. If the ModuleScienceContainer is NOT
                 * the first element in the part.Modules array, then the part's right-click menu
                 * will fail to work and you'll get NullReferenceException errors.
                 * It's important to know that the part.cfg file that contains a bunch of MODULE
                 * nodes will have its MODULE nodes loaded in the order that they appear in the file.
                 * So if the first MODULE in the file is, say, a ModuleLight, the second is a ModuleScienceContainer,
                 * and the third is a ModuleScienceLab, then make sure that containerModuleIndex is set to 1 (the array of PartModules is 0-based).
                 * 
                 * Now, with WBIModuleSwitcher, we have the added complexity of dynamically adding the ModuleScienceContainer.
                 * We won't know what the index of the ModuleScienceContainer is at runtime until after we're done
                 * dynamically adding the PartModules identified in the template. 
                 * So, now we will go through all the PartModules and find the index of the ModuleScienceContainer, and then we'll go through and find the
                 * ModuleScienceLab. If we find one, then we'll set its containerModuleIndex to the index we recorded for
                 * the ModuleScienceContainer. This code makes the assumption that the part author added a ModuleScienceContainer to the config file and then
                 * immediately after, added a ModuleScienceLab. It would get ugly if that wasn't the case.
                 */
                for (int curIndex = 0; curIndex < this.part.Modules.Count; curIndex++)
                {
                    //Get the module
                    module = this.part.Modules[curIndex];

                    //If we have a ModuleScienceContainer, then record its index.
                    if (module.moduleName == "ModuleScienceContainer")
                    {
                        containerIndex = curIndex;
                    }

                    //If we have a MobileScienceLab and we found the container index
                    //Then set the science lab's containerModuleIndex to the proper index value
                    else if (module.moduleName == "ModuleScienceLab" && containerIndex != -1)
                    {
                        //Set the index
                        sciLab = (ModuleScienceLab)module;
                        sciLab.containerModuleIndex = containerIndex;

                        Log("Science lab container index: " + sciLab.containerModuleIndex);
                        Log("scilab index " + curIndex);

                        //Reset the recorded index
                        containerIndex = -1;
                    }
                }
        }

        protected virtual void loadModulesFromTemplate(ConfigNode templateNode)
        {
            Log("loadModulesFromTemplate called");
            ConfigNode[] moduleNodes;
            string moduleName;
            PartModule module;

            try
            {
                moduleNodes = templateNode.GetNodes("MODULE");
                if (moduleNodes == null)
                {
                    Log("loadModulesFromTemplate - moduleNodes is null! Cannot proceed.");
                    return;
                }

                //Remove any previously added modules
                foreach (PartModule doomed in addedPartModules)
                {
                    this.part.RemoveModule(doomed);
                }
                addedPartModules.Clear();

                //Add the modules
                foreach (ConfigNode moduleNode in moduleNodes)
                {
                    moduleName = moduleNode.GetValue("name");
                    Log("Checking " + moduleName);

                    //Special case: ModuleScienceLab
                    //If we add ModuleScienceLab in the editor, even if we fix up its index for the ModuleScienceContainer,
                    //We get an NRE. The fix below does not work in the editor, and the right-click menu will be broken.
                    //Why? I dunno, so when in the editor we won't dynamically add the ModuleScienceLab.
                    if ((moduleName == "ModuleScienceLab" || moduleName == "ModuleScienceContainer") && HighLogic.LoadedSceneIsEditor)
                        continue;

                    //If we don't find the module on our ignore list then add it.
                    if (_ignoreTemplateModules.Contains(moduleName) == false)
                    {
                        //Add the module to the part's module list
                        module = this.part.AddModule(moduleNode);

                        //Add the module to our list
                        if (module != null)
                        {
                            addedPartModules.Add(module);

                            Log("Added " + moduleName);
                        }
                    }
                }

                fixModuleIndexes();
            }
            catch (Exception ex)
            {
                Log("loadModulesFromTemplate encountered an error: " + ex);
            }
        }
        #endregion
    }
}
