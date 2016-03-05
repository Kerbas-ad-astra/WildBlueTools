using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

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
    public class WBIModuleSwitcher : WBIResourceSwitcher
    {
        protected List<PartModule> addedPartModules = new List<PartModule>();
        protected List<ConfigNode> moduleSettings = new List<ConfigNode>();

        private bool _showGUI = true;
        private string _ignoreTemplateModules = "None";

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
            if (moduleNodes == null)
                return;

            //Save the module settings, we'll need these for later.
            foreach (ConfigNode moduleNode in moduleNodes)
                moduleSettings.Add(moduleNode);
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
                //Create a node for the module
                saveNode = ConfigNode.CreateConfigFromObject(addedModule);
                if (saveNode == null)
                {
                    Log("save node is null");
                    continue;
                }

                //Tell the module to save its data
                saveNode.name = "WBIMODULE";
                addedModule.Save(saveNode);

                //Add it to our node
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

        #endregion

        #region Helpers
        protected void loadModuleSettings(PartModule module, ConfigNode moduleNode, int index)
        {
            if (HighLogic.LoadedSceneIsFlight == false)
                return;

            Log("loadModuleSettings called");
            if (index > moduleSettings.Count - 1)
            {
                Log("Index > moduleSettings.Count!");
                return;
            }
            ConfigNode nodeSettings = moduleSettings[index];

            //Add any missing settings
            foreach (ConfigNode.Value nodeValue in moduleNode.values)
            {
                if (nodeSettings.HasValue(nodeValue.name) == false)
                    nodeSettings.AddValue(nodeValue.name, nodeValue.value);
            }

            //nodeSettings may have persistent fields. If so, then set them.
            foreach (ConfigNode.Value nodeValue in nodeSettings.values)
            {
                try
                {
                    if (nodeValue.name != "name")
                        moduleNode.SetValue(nodeValue.name, nodeValue.value, true);

                    if (module.Fields[nodeValue.name] != null)
                    {
                        Log("Set Field " + nodeValue.name + " to " + nodeValue.value);
                        module.Fields[nodeValue.name].Read(nodeValue.value, module);
                    }
                }
                catch (Exception ex)
                {
                    Log("Encountered an exception while setting values for " + moduleNode.GetValue("name") + ": " + ex);
                    continue;
                }
            }

            //Actions
            if (nodeSettings.HasNode("ACTIONS"))
            {
                ConfigNode actionsNode = nodeSettings.GetNode("ACTIONS");
                BaseAction action;

                foreach (ConfigNode node in actionsNode.nodes)
                {
                    action = module.Actions[node.name];
                    if (action != null)
                    {
                        action.actionGroup = (KSPActionGroup)Enum.Parse(typeof(KSPActionGroup), node.GetValue("actionGroup"));
                        Log("Set " + node.name + " to " + action.actionGroup);
                    }
                }
            }
        }

        protected void loadModuleSettings(PartModule module, int index)
        {
            Log("loadModuleSettings called");
            if (index > moduleSettings.Count - 1)
            {
                Log("Index > moduleSettings.Count!");
                return;
            }
            ConfigNode nodeSettings = moduleSettings[index];

            //nodeSettings may have persistent fields. If so, then set them.
            foreach (ConfigNode.Value nodeValue in nodeSettings.values)
            {
                if (nodeValue.name != "name")
                {
                    if (module.Fields[nodeValue.name] != null)
                        module.Fields[nodeValue.name].Read(nodeValue.value, module);
                    Log("Set " + nodeValue.name + " to " + nodeValue.value);
                }
            }
        }

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

        protected bool canLoadModule(ConfigNode node)
        {
            string value;

            //If we are in career mode, make sure we have unlocked the tech node.
            if (ResearchAndDevelopment.Instance != null)
            {
                value = node.GetValue("TechRequired");
                if (!string.IsNullOrEmpty(value) && (HighLogic.CurrentGame.Mode == Game.Modes.CAREER || HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX))
                {
                    if (ResearchAndDevelopment.GetTechnologyState(value) != RDTech.State.Available)
                        return false;
                }
            }

            //Now check for required mod
            value = node.GetValue("needs");
            if (!string.IsNullOrEmpty(value))
            {
                if (TemplatesModel.CheckNeeds(value) != EInvalidTemplateReasons.TemplateIsValid)
                    return false;
            }

            return true;
        }

        protected virtual void loadModulesFromTemplate(ConfigNode templateNode)
        {
            Log("loadModulesFromTemplate called for template: " + templateNode.GetValue("shortName"));
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

                    //Make sure we can load the module
                    if (canLoadModule(moduleNode) == false)
                        continue;

                    //Special case: ModuleScienceLab
                    //If we add ModuleScienceLab in the editor, even if we fix up its index for the ModuleScienceContainer,
                    //We get an NRE. The fix below does not work in the editor, and the right-click menu will be broken.
                    //Why? I dunno, so when in the editor we won't dynamically add the ModuleScienceLab.
                    if ((moduleName == "ModuleScienceLab" || moduleName == "ModuleScienceContainer") && HighLogic.LoadedSceneIsEditor)
                        continue;

                    //If we don't find the module on our ignore list then add it.
                    if (_ignoreTemplateModules.Contains(moduleName) == false)
                    {
                        //module = this.part.AddModule(moduleNode);

                        //Courtesy of http://forum.kerbalspaceprogram.com/threads/27851-part-AddModule%28ConfigNode-node%29-NullReferenceException-in-PartModule-Load%28node%29-help
                        module = this.part.AddModule(moduleName);
                        if (module == null)
                            continue;

                        //Add the module to our list
                        addedPartModules.Add(module);

                        //Now wake up the module
                        object[] parameters = new object[] { };
                        MethodInfo awakenMethod = typeof(PartModule).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (awakenMethod == null)
                        {
                            Log("No awaken method!");
                            continue;
                        }
                        awakenMethod.Invoke(module, parameters);
                        module.OnAwake();
                        module.OnActive();
                        
                        //Load up the config
                        loadModuleSettings(module, moduleNode, addedPartModules.Count - 1);
                        Log("Calling module.Load");
                        module.Load(moduleNode);

                        //Start it up
                        Log("calling module.OnStart with state: " + this.part.vessel.situation);
                        if (HighLogic.LoadedSceneIsFlight)
                        {
                            switch (this.part.vessel.situation)
                            {
                                case Vessel.Situations.ORBITING:
                                    module.OnStart(PartModule.StartState.Orbital);
                                    break;
                                case Vessel.Situations.LANDED:
                                    module.OnStart(PartModule.StartState.Landed);
                                    break;
                                case Vessel.Situations.SPLASHED:
                                    module.OnStart(PartModule.StartState.Splashed);
                                    break;

                                case Vessel.Situations.SUB_ORBITAL:
                                    module.OnStart(PartModule.StartState.SubOrbital);
                                    break;

                                case Vessel.Situations.FLYING:
                                    module.OnStart(PartModule.StartState.Flying);
                                    break;

                                default:
                                    module.OnStart(PartModule.StartState.None);
                                    break;
                            }
                        }

                        else
                        {
                            module.OnStart(PartModule.StartState.None);
                        }

                        Log("Added " + moduleName);
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
