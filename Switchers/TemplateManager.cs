using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
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
    public enum EInvalidTemplateReasons
    {
        TemplateIsValid,
        TechNotUnlocked,
        InvalidIndex,
        RequiredModuleNotFound,
        NoTemplates
    }

    public class TemplateManager
    {
        public Part part = null;
        public Vessel vessel = null;
        public LogDelegate logDelegate = null;
        public ConfigNode[] templateNodes;
        private string _templateNodeName;
        private string _templateTags;
        private static List<string> partTokens;

        #region API
        public string templateTags
        {
            get
            {
                return _templateTags;
            }

            set
            {
                _templateTags = value;
            }
        }

        public string templateNodeName
        {
            get
            {
                return _templateNodeName;
            }

            set
            {
                _templateNodeName = value;
                List<ConfigNode> templates = new List<ConfigNode>();
                string[] potentialTemplates = _templateNodeName.Split(new char[] { ';' });
                ConfigNode[] templateConfigs = GameDatabase.Instance.GetConfigNodes(_templateNodeName);
                string needs;

                foreach (string potentialTemplate in potentialTemplates)
                {
                    templateConfigs = GameDatabase.Instance.GetConfigNodes(potentialTemplate);
                    if (templateConfigs == null)
                        continue;

                    //Check to see if the template needs a specific mod
                    foreach (ConfigNode config in templateConfigs)
                    {
                        needs = config.GetValue("needs");
                        if (needs == null)
                            templates.Add(config);
                        else if (TemplateManager.CheckNeeds(needs) == EInvalidTemplateReasons.TemplateIsValid)
                            templates.Add(config);
                    }
                }

                //Done
                this.templateNodes = templates.ToArray();
                Log(_templateNodeName + " has " + templates.Count + " templates.");
                ConfigNode node;
                for (int index = 0; index < this.templateNodes.Length; index++)
                {
                    node = this.templateNodes[index];
                    Log("Template " + index + ": " + node.GetValue("shortName"));
                }
            }
        }

        public TemplateManager(Part part, Vessel vessel, LogDelegate logDelegate, string template = "nodeTemplate", string templateTags = "templateTags")
        {
            this.part = part;
            this.vessel = vessel;
            this.logDelegate = logDelegate;

            _templateNodeName = template;
            _templateTags = templateTags;

            this.templateNodes = GameDatabase.Instance.GetConfigNodes(template);
            if (templateNodes == null)
            {
                Log("nodeTemplatesModel templateNodes == null!");
                return;
            }
        }

        public ConfigNode this[string templateName]
        {
            get
            {
                int index = FindIndexOfTemplate(templateName);

                return this.templateNodes[index];
            }
        }

        public ConfigNode this[long index]
        {
            get
            {
                return this.templateNodes[index];
            }
        }

        public static EInvalidTemplateReasons CheckNeeds(string neededMod)
        {
            string modToCheck = null;
            bool checkInverse = false;
            bool modFound = false;

            //Create the part tokens if needed
            if (partTokens == null)
            {
                partTokens = new List<string>();
                string url;
                UrlDir.UrlConfig[] allConfigs = GameDatabase.Instance.root.AllConfigs.ToArray();
                char[] delimiters = { '/' };
                string[] tokens;

                foreach (UrlDir.UrlConfig config in allConfigs)
                {
                    if (config.parent.url.Contains("Squad"))
                        continue;

                    url = config.parent.url.Substring(0, config.parent.url.LastIndexOf("/"));
                    tokens = url.Split(delimiters);
                    foreach (string token in tokens)
                    {
                        if (partTokens.Contains(token) == false)
                            partTokens.Add(token);
                    }
                }
            }

            //Now check for the required mod
            modToCheck = neededMod;
            if (neededMod.StartsWith("!"))
            {
                checkInverse = true;
                modToCheck = neededMod.Substring(1, neededMod.Length - 1);
            }

            modFound = partTokens.Contains(modToCheck);
            if (modFound && checkInverse == false)
                return EInvalidTemplateReasons.TemplateIsValid;
            else if (modFound && checkInverse)
                return EInvalidTemplateReasons.RequiredModuleNotFound;
            else if (!modFound && checkInverse)
                return EInvalidTemplateReasons.TemplateIsValid;
            else
                return EInvalidTemplateReasons.RequiredModuleNotFound;
        }

        public EInvalidTemplateReasons CanUseTemplate(ConfigNode nodeTemplate)
        {
            string value;
            PartModule requiredModule;
            EInvalidTemplateReasons invalidTemplateReason;

            //Make sure the vessel object is set
            if (this.vessel == null)
                this.vessel = this.part.vessel;

            //If we are in career mode, make sure we have unlocked the tech node.
            if (ResearchAndDevelopment.Instance != null)
            {
                value = nodeTemplate.GetValue("TechRequired");
                if (string.IsNullOrEmpty(value))
                    return EInvalidTemplateReasons.TemplateIsValid;

                if (ResearchAndDevelopment.GetTechnologyState(value) != RDTech.State.Available)
                    return EInvalidTemplateReasons.TechNotUnlocked;
            }

            //If we need a specific mod then check for it.
            value = nodeTemplate.GetValue("needs");
            if (string.IsNullOrEmpty(value) == false)
            {
                invalidTemplateReason = TemplateManager.CheckNeeds(value);

                if (invalidTemplateReason != EInvalidTemplateReasons.TemplateIsValid)
                    return invalidTemplateReason;
            }

            //If we need a specific module then check for it.
            value = nodeTemplate.GetValue("requiresModule");
            if (string.IsNullOrEmpty(value) == false)
            {
                requiredModule = this.part.Modules[value];
                if (requiredModule == null)
                {
                    return EInvalidTemplateReasons.RequiredModuleNotFound;
                }
            }

            //If we need a specific template type then check for it.
            value = nodeTemplate.GetValue("templateTags");
            if (string.IsNullOrEmpty(value) == false)
            {
                //if we have template types then see if the templateTags is in the list.
                //Otherwise, we're good.
                if (string.IsNullOrEmpty(_templateTags) == false)
                {
                    if (_templateTags.Contains(value) == false)
                    {
                        return EInvalidTemplateReasons.RequiredModuleNotFound;
                    }
                }
            }

            //If we're in the editor, then that's all we need to check.
            if (HighLogic.LoadedSceneIsEditor)
                return EInvalidTemplateReasons.TemplateIsValid;

            return EInvalidTemplateReasons.TemplateIsValid;
        }

        public EInvalidTemplateReasons CanUseTemplate(string templateName)
        {
            int index = FindIndexOfTemplate(templateName);

            return CanUseTemplate(index);
        }

        public EInvalidTemplateReasons CanUseTemplate(int index)
        {
            if (this.templateNodes == null)
                return EInvalidTemplateReasons.NoTemplates;

            if (index < 0 || index > templateNodes.Count<ConfigNode>())
                return EInvalidTemplateReasons.InvalidIndex;

            return CanUseTemplate(templateNodes[index]);
        }

        public int FindIndexOfTemplate(string templateName)
        {
            int templateIndex = -1;
            int totalTemplates = -1;
            string shortName;

            //Get total template count
            if (this.templateNodes == null)
                return -1;
            totalTemplates = this.templateNodes.Count<ConfigNode>();

            //Loop through the templates and find the one matching the desired template name
            //the GUI friendly shortName
            for (templateIndex = 0; templateIndex < totalTemplates; templateIndex++)
            {
                shortName = this.templateNodes[templateIndex].GetValue("shortName");
                if (!string.IsNullOrEmpty(shortName))
                {
                    if (shortName == templateName)
                        return templateIndex;
                }
            }

            return -1;
        }

        public int GetPrevTemplateIndex(int startIndex)
        {
            int prevIndex = startIndex;

            if (this.templateNodes == null)
                return -1;

            if (this.templateNodes.Count<ConfigNode>() == 0)
                return -1;

            //Get prev index in template array
            prevIndex = prevIndex - 1;
            if (prevIndex < 0)
                prevIndex = this.templateNodes.Count<ConfigNode>() - 1;

            return prevIndex;
        }

        public int GetNextTemplateIndex(int startIndex)
        {
            int nextIndex = startIndex;

            if (this.templateNodes == null)
                return -1;

            if (this.templateNodes.Count<ConfigNode>() == 0)
                return -1;

            //Get next index in template array
            nextIndex = (nextIndex + 1) % this.templateNodes.Count<ConfigNode>();

            return nextIndex;
        }

        public int GetPrevUsableIndex(int startIndex)
        {
            int totalTries = this.templateNodes.Count<ConfigNode>();
            int prevIndex = startIndex;
            ConfigNode template;

            do
            {
                prevIndex = GetPrevTemplateIndex(prevIndex);
                template = this[prevIndex];
                totalTries -= 1;

                if (CanUseTemplate(template) == EInvalidTemplateReasons.TemplateIsValid)
                    return prevIndex;
            }

            while (totalTries > 0);

            return -1;
        }

        public int GetNextUsableIndex(int startIndex)
        {
            int totalTries = this.templateNodes.Count<ConfigNode>();
            int nextIndex = startIndex;
            ConfigNode template;

            do
            {
                nextIndex = GetNextTemplateIndex(nextIndex);
                template = this[nextIndex];
                totalTries -= 1;

                if (CanUseTemplate(template) == EInvalidTemplateReasons.TemplateIsValid)
                    return nextIndex;
            }

            while (totalTries > 0);        

            return -1;
        }

        #endregion

        #region Helpers
        public virtual void Log(object message)
        {
            if (logDelegate != null)
                logDelegate(message);
        }
        #endregion
    }
}
