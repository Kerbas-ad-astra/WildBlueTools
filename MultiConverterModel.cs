using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
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
    public class MultiConverterModel
    {
        public LogDelegate logDelegate = null;
        public Part part = null;
        public Vessel vessel = null;
        public List<ModuleResourceConverter> converters;

        protected Dictionary<string, bool> enabledStates = new Dictionary<string, bool>();

        #region API
        public MultiConverterModel(Part part, Vessel vessel, LogDelegate logDelegate)
        {
            this.part = part;
            this.vessel = vessel;
            this.logDelegate = logDelegate;

            this.converters = new List<ModuleResourceConverter>();
        }

        public void Clear()
        {
            List<PartModule> doomedModules = new List<PartModule>();

            foreach (PartModule module in this.part.Modules)
            {
                if (module.name.Contains("ModuleResourceConverter"))
                    doomedModules.Add(module);
            }

            foreach (PartModule doomed in doomedModules)
                this.part.RemoveModule(doomed);

            this.converters.Clear();
        }

        public void Load(ConfigNode node)
        {
            Log("[MultiConverterModel] Load called.");
            ConfigNode[] converterNodes = node.GetNodes("ConverterState");

            if (converterNodes == null)
            {
                Log("converter states are null");
                return;
            }

            foreach (ConfigNode converterNode in converterNodes)
                enabledStates.Add(converterNode.GetValue("ConverterName"), bool.Parse(converterNode.GetValue("IsActivated")));
        }

        public void Save(ConfigNode node)
        {
            Log("[MultiConverterModel] Save called.");
            ConfigNode converterNode;

            foreach (ModuleResourceConverter converter in converters)
            {
                //Generate a new config node
                converterNode = ConfigNode.CreateConfigFromObject(converter);
                converterNode.name = "ConverterState";

                //Save the converter's data
                SaveConverterState(converter, converterNode);

                //Add converter node to the node we're saving to.
                node.AddNode(converterNode);
            }
        }

        public void LoadConvertersFromTemplate(ConfigNode nodeTemplate)
        {
            Log("LoadConvertersFromTemplate called.");
            ConfigNode[] templateModules = nodeTemplate.GetNodes("MODULE");
            string value;

            //Sanity check
            if (templateModules == null)
                return;

            //Clear existing nodes
            Clear();

            //Go through each module node and look for a ModuleResourceConverter.
            //If found, set up a new converter.
            foreach (ConfigNode nodeModule in templateModules)
            {
                value = nodeModule.GetValue("name");
                if (string.IsNullOrEmpty(value))
                    continue;

                //Found a converter?
                //load up a new converter using the template's parameters.
                if (value == "ModuleResourceConverter")
                    AddFromTemplate(nodeModule);
            }
        }

        public ModuleResourceConverter AddFromTemplate(ConfigNode node)
        {
            Log("AddFromTemplate called for converter: " + node.GetValue("ConverterName"));

            string value = node.GetValue("needs");
            if (string.IsNullOrEmpty(value) == false)
                if (TemplatesModel.CheckNeeds(value) == EInvalidTemplateReasons.RequiredModuleNotFound)
                    return null;

            //Courtesy of http://forum.kerbalspaceprogram.com/threads/27851-part-AddModule%28ConfigNode-node%29-NullReferenceException-in-PartModule-Load%28node%29-help
            ModuleResourceConverter converter = (ModuleResourceConverter)this.part.AddModule(node.GetValue("name"));
            object[] parameters = new object[] { };
            MethodInfo awakenMethod = typeof(PartModule).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            if (awakenMethod == null)
            {
                Log("No awaken method!");
                return null;
            }
            awakenMethod.Invoke(converter, parameters);
            converter.Load(node);
            converter.OnAwake();
            converter.OnActive();
            converter.EnableModule();

            /*
            foreach (BaseField field in converter.Fields)
            {
                Debug.Log("field.name: " + field.name);
                Debug.Log("value: " + field.GetValue(field.host));
            }
             */

            //Remove the converter's GUI
            RunHeadless(converter);

            //Add it to the list
            this.converters.Add(converter);
            Debug.Log("Added converter " + converter.ConverterName);

            return converter;
        }

        public string GetRequirements(int index)
        {
            ConfigNode[] templateNodes = GameDatabase.Instance.GetConfigNodes("nodeTemplate");

            if (templateNodes == null)
                return "";
            if (index < 0 || index > templateNodes.Count<ConfigNode>())
                return "";

            return GetRequirements(templateNodes[index]);
        }

        public string GetRequirements(ConfigNode templateNode)
        {
            StringBuilder requirements = new StringBuilder();
            Dictionary<string, float> totalRequirements = new Dictionary<string, float>();
            float amount;
            string converterRequirements = null;
            string value;
            ConfigNode[] requiredResources;

            try
            {
                //Find all the ModuleResourceConverter nodes and sum up their require resources.
                foreach (ConfigNode converterNode in templateNode.nodes)
                {
                    //Need a ModuleResourceConverter
                    value = converterNode.GetValue("name");
                    if (string.IsNullOrEmpty(value))
                        continue;
                    if (value != "ModuleResourceConverter")
                        continue;

                    //Ok, now get the required resources
                    requiredResources = converterNode.GetNodes("REQUIRED_RESOURCE");
                    foreach (ConfigNode requiredResource in requiredResources)
                    {
                        value = requiredResource.GetValue("ResourceName");
                        amount = float.Parse(requiredResource.GetValue("Ratio"));

                        //Either add the resource to the dictionary, or set the greater of new amount or existing amount
                        if (totalRequirements.ContainsKey(value))
                            totalRequirements[value] = amount > totalRequirements[value] ? amount : totalRequirements[value];
                        else
                            totalRequirements.Add(value, amount);
                    }
                }

                //Now fill out the stringbuilder
                foreach (string key in totalRequirements.Keys)
                {
                    requirements.Append(String.Format("{0:#,###.##}", totalRequirements[key]));
                    requirements.Append(" " + key + " , ");
                }

                //Strip off the last " , " characters
                converterRequirements = requirements.ToString();
                if (!string.IsNullOrEmpty(converterRequirements))
                {
                    converterRequirements = converterRequirements.Substring(0, converterRequirements.Length - 3);
                    return converterRequirements;
                }
            }
            catch (Exception ex)
            {
                Log("getConverterRequirements ERROR: " + ex);
            }

            return "nothing";
        }

        public void OnStart(PartModule.StartState state)
        {
            Log("[MulticonverterModel] OnStart - State: " + state);

            //Set their active/inactive states from the data we collected during loading.
            foreach (ModuleResourceConverter converter in converters)
            {
                if (enabledStates.ContainsKey(converter.ConverterName))
                    converter.IsActivated = enabledStates[converter.ConverterName];

                if (converter.IsActivated)
                    converter.StartResourceConverter();
            }

            //Cleanup
            enabledStates.Clear();
        }

        #endregion

        #region Helpers

        public void SaveConverterState(ModuleResourceConverter converter, ConfigNode node)
        {
            //Save the converter's name and activation state.
            node.AddValue("ConverterName", converter.ConverterName);
            node.AddValue("IsActivated", converter.IsActivated.ToString());
        }

        public void RunHeadless(ModuleResourceConverter converter)
        {
            Log("RunHeadless called.");

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

        public virtual void Log(object message)
        {
            if (logDelegate != null)
                logDelegate(message);
        }
        #endregion
    }
}
