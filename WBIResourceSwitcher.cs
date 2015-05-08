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
    public class WBIResourceSwitcher : WBIInflatablePartModule
    {
        private static string MAIN_TEXTURE = "_MainTex";
        private static string EMISSIVE_TEXTURE = "_Emissive";

        //Index of the current module template we're using.
        public int CurrentTemplateIndex;

        //Determines whether or not the resource container can be reconfigured in the field.
        public bool fieldReconfigurable = false;

        //Decal names (these are the names of the graphics assets, including file path)
        protected string logoPanelName;
        protected string glowPanelName;

        //Name of the transform(s) for the colony decal.
        //These names come from the model itself.
        private string _logoPanelTransforms;

        //List of resources that we are allowed to clear when performing a template switch.
        //If set to ALL, then all of the part's resources will be cleared.
        private string _resourcesToReplace = "ALL";

        //Name of the template nodes.
        private string _templateNodes;

        //Name of the template types allowed
        private string _templateTypes;

        //Used when, say, we're in the editor, and we don't get no game-saved values from perisistent.
        private string _defaultTemplate;

        //Base location to the decals
        private string _decalBasePath;

        //Base amount of KAS the part stores, if any.
        private int _baseKasAmount;

        //Since not all storage containers are equal, the
        //capacityFactor is used to determine how much of the template's base resource amount
        //applies to the container.
        [KSPField(isPersistant = true)]
        public float capacityFactor = 0f;

        //Helper objects
        protected string techRequiredToReconfigure;
        protected string capacityFactorTypes;
        protected int kasAmount;
        protected bool confirmResourceSwitch = false;
        protected bool deflateConfirmed = false;
        protected TemplatesModel templatesModel;
        protected Dictionary<string, ConfigNode> parameterOverrides = new Dictionary<string, ConfigNode>();
        protected Dictionary<string, double> resourceMaxAmounts = new Dictionary<string, double>();
        private List<PartResource> _templateResources = new List<PartResource>();
        private bool _switchClickedOnce = false;

        #region Display Fields
        //We use this field to identify the template config node as well as have a GUI friendly name for the user.
        //When the module starts, we'll use the shortName to find the template and get the info we need.
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Module Type")]
        public string shortName;
        #endregion

        #region User Events & API
        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Next Type", active = true, externalToEVAOnly = false, unfocusedRange = 3.0f, guiActiveUnfocused = true)]
        public void NextType()
        {
            if (confirmResourceSwitch && HighLogic.LoadedSceneIsFlight)
            {
                if (_switchClickedOnce == false)
                {
                    ScreenMessages.PostScreenMessage("Existing resources will be removed. Click a second time to confirm switch.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    _switchClickedOnce = true;
                    return;
                }

                _switchClickedOnce = false;
            }

            int templateIndex = templatesModel.GetNextUsableIndex(CurrentTemplateIndex);

            if (templateIndex != -1)
            {
                UpdateContentsAndGui(templateIndex);
                UpdateSymmetry(templateIndex);
                return;
            }

            //If we reach here then something went wrong.
            ScreenMessages.PostScreenMessage("Unable to find a template to switch to.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
        }

        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Prev Type", active = true, externalToEVAOnly = false, unfocusedRange = 3.0f, guiActiveUnfocused = true)]
        public void PrevType()
        {
            if (confirmResourceSwitch && HighLogic.LoadedSceneIsFlight)
            {
                if (_switchClickedOnce == false)
                {
                    ScreenMessages.PostScreenMessage("Existing resources will be removed. Click a second time to confirm switch.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    _switchClickedOnce = true;
                    return;
                }

                _switchClickedOnce = false;
            }

            int templateIndex = templatesModel.GetPrevUsableIndex(CurrentTemplateIndex);

            if (templateIndex != -1)
            {
                UpdateContentsAndGui(templateIndex);
                UpdateSymmetry(templateIndex);
                return;
            }

            //If we reach here then something went wrong.
            ScreenMessages.PostScreenMessage("Unable to find a template to switch to.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
        }

        public void ReloadTemplate()
        {
            if (CurrentTemplateIndex != -1)
            {
                UpdateContentsAndGui(CurrentTemplateIndex);
                UpdateSymmetry(CurrentTemplateIndex);
            }
        }

        public string CurrentTemplateName
        {
            get
            {
                ConfigNode currentTemplate = templatesModel[CurrentTemplateIndex];

                if (currentTemplate != null)
                    return currentTemplate.GetValue("shortName");
                else
                    return "Unknown";
            }
        }

        public ConfigNode CurrentTemplate
        {
            get
            {
                return templatesModel[CurrentTemplateIndex];
            }
        }

        public virtual void UpdateContentsAndGui(string templateName)
        {
            int index = templatesModel.FindIndexOfTemplate(templateName);

            UpdateContentsAndGui(index);
        }

        public virtual void UpdateSymmetry(int templateIndex)
        {
            WBIResourceSwitcher resourceSwitcher;

            //Finally, load resources for symmetrical parts
            if (HighLogic.LoadedSceneIsEditor)
            {
                foreach (Part symmetryPart in this.part.symmetryCounterparts)
                {
                    resourceSwitcher = symmetryPart.GetComponent<WBIResourceSwitcher>();
                    resourceSwitcher.UpdateContentsAndGui(templateIndex);
                }
            }

            //Dirty the GUI
            UIPartActionWindow tweakableUI = Utils.FindActionWindow(this.part);
            if (tweakableUI != null)
                tweakableUI.displayDirty = true;
        }

        public virtual void UpdateContentsAndGui(int templateIndex)
        {
            string name;
            if (templatesModel.templateNodes == null)
            {
                Log("NextModuleType templateNodes == null!");
                return;
            }

            //Make sure we have a valid index
            if (templateIndex == -1)
                return;

            //Ok, we're good
            CurrentTemplateIndex = templateIndex;

            //Set the current template name
            shortName = templatesModel[templateIndex].GetValue("shortName");
            if (string.IsNullOrEmpty(shortName))
                return;

            //Change the toggle buttons' names
            templateIndex = templatesModel.GetNextUsableIndex(CurrentTemplateIndex);
            if (templateIndex != -1 && templateIndex != CurrentTemplateIndex)
            {
                name = templatesModel[templateIndex].GetValue("shortName");
                Events["NextType"].guiName = "Next: " + name;
            }

            templateIndex = templatesModel.GetPrevUsableIndex(CurrentTemplateIndex);
            if (templateIndex != -1 && templateIndex != CurrentTemplateIndex)
            {
                name = templatesModel[templateIndex].GetValue("shortName");
                Events["PrevType"].guiName = "Prev: " + name;
            }

            //Set up the module in its new configuration
            RedecorateModule();

            //Update the resource panel
            if (HighLogic.LoadedSceneIsFlight && ResourceDisplay.Instance != null)
            {
                ResourceDisplay.Instance.Refresh();
                ResourceDisplay.Instance.Update();
            }
        }

        public virtual void RedecorateModule(bool payForRedecoration = true, bool loadTemplateResources = true)
        {
            try
            {
                Log("RedecorateModule called. payForRedecoration: " + payForRedecoration.ToString() + " loadTemplateResources: " + loadTemplateResources.ToString());
                if (templatesModel == null)
                    return;
                if (templatesModel.templateNodes == null)
                    return;

                ConfigNode nodeTemplate = templatesModel[CurrentTemplateIndex];
                if (nodeTemplate == null)
                    return;

                //Get max resource amounts if the part is inflatable.
                if (isInflatable)
                {
                    //Clear our max amounts dictionary
                    resourceMaxAmounts.Clear();

                    //Get all the resources in the template and add their max amounts
                    ConfigNode[] templateResourceNodes = nodeTemplate.GetNodes("RESOURCE");
                    if (templateResourceNodes != null)
                    {
                        //Set the max amounts into our dictionary.
                        foreach (ConfigNode resourceNode in templateResourceNodes)
                        {
                            resourceMaxAmounts.Add(resourceNode.GetValue("name"), double.Parse(resourceNode.GetValue("maxAmount")) * capacityFactor);
                        }
                    }
                }

                //Load the template resources into the module.
                OnEditorAttach();
                if (loadTemplateResources)
                    loadResourcesFromTemplate(nodeTemplate);

                //Call the OnRedecorateModule method to give others a chance to do stuff
                OnRedecorateModule(nodeTemplate, payForRedecoration);

                //Finally, change the decals on the part.
                updateDecalsFromTemplate(nodeTemplate);

                Log("Module redecorated.");
            }
            catch (Exception ex)
            {
                Log("RedecorateModule encountered an ERROR: " + ex);
            }
        }

        /*
        float IPartMassModifier.GetModuleMass(float defaultMass)
        {
            List<PartResource> resourceList = this.part.Resources.list;
            float resourceMass = ResourceHelper.GetResourceMass(this.part);

            return defaultMass + resourceMass;
        }

        float IPartCostModifier.GetModuleCost(float defaultCost)
        {
            List<PartResource> resourceList = this.part.Resources.list;
            float resourceCost = ResourceHelper.GetResourceCost(this.part);

            return defaultCost + resourceCost;
        }
        */

        #endregion

        #region Module Overrides
        public override string GetInfo()
        {
            return "Check the tweakables menu for the different resources that the tank can hold.";
        }

        public override void ToggleAnimation()
        {
            Log("ToggleAnimation called.");
            List<PartResource> resourceList = this.part.Resources.list;
            PartModule kasContainer = this.part.Modules["KASModuleContainer"];

            //If the module cannot be deflated then exit.
            if (CanBeDeflated() == false)
            {
                Log("ToggleAnimation: Not deflating module.");
                return;
            }
            base.ToggleAnimation();
            deflateConfirmed = false;

            //If the module is now inflated, re-add the max resource amounts to the list of resources.
            //If it isn't inflated, set max amount to 1.
            foreach (PartResource resource in resourceList)
            {
                //If we are deployed then reset the max amounts.
                if (isDeployed)
                {
                    if (resourceMaxAmounts.ContainsKey(resource.resourceName))
                    {
                        Log("resource " + resource.resourceName + " found.");
                        resource.amount = 0;
                        resource.maxAmount = resourceMaxAmounts[resource.resourceName];
                        Log("max amount: " + resourceMaxAmounts[resource.resourceName]);
                    }
                }

                else //No longer deployed.
                {
                    resource.amount = 0;
                    resource.maxAmount = 1;
                }
            }

            //KAS container
            if (kasContainer != null)
            {
                if (isDeployed)
                    Utils.SetField("maxSize", kasAmount, kasContainer);
                else
                    Utils.SetField("maxSize", 1, kasContainer);
            }
        }

        public virtual bool HasResources()
        {
            List<PartResource> resourceList = this.part.Resources.list;

            if (HighLogic.LoadedSceneIsEditor == false)
            {
                foreach (PartResource res in resourceList)
                {
                    if (res.amount > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public virtual bool CanBeDeflated()
        {
            List<PartResource> resourceList = this.part.Resources.list;

            if (HighLogic.LoadedSceneIsEditor == false)
            {
                //If the module is inflatable, deployed, and has kerbals inside, then don't allow the module to be deflated.
                if (isInflatable && isDeployed && this.part.protoModuleCrew.Count() > 0)
                {
                    Log("CanBeDeflated: Module has crew aboard, cannot be deflated.");
                    ScreenMessages.PostScreenMessage(this.part.partName + " has crew aboard. Vacate the module before deflating it.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    return false;
                }

                //If the module is inflatable, deployed, has resources, and user hasn't confirmed yet, then get confirmation that user wants to deflate the module.
                if (HasResources() && isDeployed && isInflatable && deflateConfirmed == false)
                {
                    Log("CanBeDeflated: Resources detected, requesting confirmation to delfate the module.");
                    deflateConfirmed = true;
                    ScreenMessages.PostScreenMessage(this.part.partName + " has resources. Click again to confirm module deflation.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    return false;
                }
            }

            Log("CanBeDeflated: Module can be deflated.");
            return true;
        }

        public virtual void OnRedecorateModule(ConfigNode nodeTemplate, bool payForRedecoration)
        {
            //Dummy method
        }

        public virtual void OnEditorAttach()
        {
        }

        public override void OnLoad(ConfigNode node)
        {
            ConfigNode[] resourceNodes = node.GetNodes("RESOURCE");
            PartResource resource = null;
            string resourceName;
            string protoNodeKey;
            string myPartName = getMyPartName();
            string value;
            ConfigNode protoNode = null;

            base.OnLoad(node);
            protoNodeKey = myPartName + this.moduleName;
            Log("OnLoad: " + myPartName + " " + node + " Scene: " + HighLogic.LoadedScene.ToString());

            //Watch for the editor attach event
            this.part.OnEditorAttach += OnEditorAttach;

            if (protoPartNodes.ContainsKey(protoNodeKey))
            {
                //Get the proto config node
                protoNode = protoPartNodes[protoNodeKey];

                //Name of the nodes to use as templates
                _templateNodes = protoNode.GetValue("templateNodes");

                //Also get template types
                _templateTypes = protoNode.GetValue("templateTypes");

                //Base KAS amounts
                value = protoNode.GetValue("baseKasAmount");
                if (string.IsNullOrEmpty(value) == false)
                    _baseKasAmount = int.Parse(value);
            }

            //Current KAS amount
            value = node.GetValue("kasAmount");
            if (string.IsNullOrEmpty(value) == false)
                kasAmount = int.Parse(value);

            //Create the templatesModel
            templatesModel = new TemplatesModel(this.part, this.vessel, new LogDelegate(Log), _templateNodes, _templateTypes);

            //If we have resources in our node then load them.
            if (resourceNodes != null)
            {
                //Clear any existing resources. We shouldn't have any...
                _templateResources.Clear();

                foreach (ConfigNode resourceNode in resourceNodes)
                {
                    resourceName = resourceNode.GetValue("name");
                    if (this.part.Resources.Contains(resourceName))
                    {
                        resource = this.part.Resources[resourceName];
                        if (isInflatable)
                        {
                            if (isDeployed)
                                resource.maxAmount = double.Parse(resourceNode.GetValue("maxAmount"));
                            else
                                resource.maxAmount = 1.0f;
                        }

                        else
                        {
                            resource.maxAmount = double.Parse(resourceNode.GetValue("maxAmount"));
                        }
                    }
                    else
                    {
                        resource = this.part.AddResource(resourceNode);
                    }

                    _templateResources.Add(resource);
                }
            }
        }

        public override void OnSave(ConfigNode node)
        {
            ConfigNode resourceNode;
            ConfigNode[] subNodes;
            string value;
            base.OnSave(node);
            bool resourceNotFound = true;

            node.AddValue("kasAmount", kasAmount.ToString());

            foreach (PartResource resource in _templateResources)
            {
                //See if the resource node already exists.
                //If it doesn't then add the new node.
                subNodes = node.GetNodes("RESOURCE");
                if (subNodes == null)
                {
                    //Create the resource node and save its data
                    resourceNode = ConfigNode.CreateConfigFromObject(resource);
                    resourceNode.name = "RESOURCE";
                    resource.Save(resourceNode);
                    node.AddNode(resourceNode);
                }

                else //Loop through the config node and add the resource if it does not exist
                {
                    resourceNotFound = true;

                    foreach (ConfigNode subNode in subNodes)
                    {
                        value = subNode.GetValue("name");
                        if (string.IsNullOrEmpty(value) == false)
                        {
                            if (value == resource.resourceName)
                            {
                                resourceNotFound = false;
                                break;
                            }
                        }
                    }

                    //Resource not found? Great, add it.
                    if (resourceNotFound)
                    {
                        //Create the resource node and save its data
                        resourceNode = ConfigNode.CreateConfigFromObject(resource);
                        resourceNode.name = "RESOURCE";
                        resource.Save(resourceNode);
                        node.AddNode(resourceNode);
                    }
                }
            }
        }

        public override void OnStart(PartModule.StartState state)
        {
            bool loadTemplateResources = _templateResources.Count<PartResource>() > 0 ? false : true;
            base.OnStart(state);
            Log("OnStart - State: " + state + "  Part: " + getMyPartName());

            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)
                return;

            //Initialize the templates
            initTemplates();

            //Hide GUI only shown in the editor
            hideEditorGUI(state);

            //Since the module will be loaded as it was originally created, we won't have 
            //the proper decals and converter settings when the module and part are loaded in flight.
            //Thus, we must redecorate to configure the module and part correctly.
            //When we do, we don't make the player pay for the redecoration, and we want to preserve
            //the part's existing resources, not to mention the current settings for the converters.
            //Also, if we have converters already then we've loaded their states during the OnLoad method call.
            RedecorateModule(false, loadTemplateResources);

            //Init the module GUI
            initModuleGUI();
        }

        #endregion

        #region Helpers
        public virtual void loadResourcesFromTemplate(ConfigNode nodeTemplate)
        {
            PartResource resource = null;
            List<PartResource> resourceList = this.part.Resources.list;
            List<PartResource> savedResources = new List<PartResource>();
            PartModule kasContainer = null;
            string value;
            string templateType = nodeTemplate.GetValue("templateType");

            Log("loadResourcesFromTemplate called for template: " + nodeTemplate.GetValue("shortName"));
            Log("template: " + nodeTemplate);
            ConfigNode[] templateResourceNodes = nodeTemplate.GetNodes("RESOURCE");
            Log("template resource count: " + templateResourceNodes.Length);
            if (templateResourceNodes == null)
            {
                Log(nodeTemplate.GetValue("shortName") + " has no resources.");
                return;
            }

            //Clear the list
            //Much quicker than removing individual resources...
            PartResource[] partResources = this.part.GetComponents<PartResource>();
            foreach (PartResource doomed in partResources)
                DestroyImmediate(doomed);
            this.part.Resources.list.Clear();
            _templateResources.Clear();

            //Add resources from template
            Log("template resource count: " + templateResourceNodes.Length);
            foreach (ConfigNode resourceNode in templateResourceNodes)
            {
                resource = this.part.AddResource(resourceNode);
                Log("Added resource: " + resource.resourceName);

                //Apply the capacity factor
                if (HighLogic.LoadedSceneIsEditor)
                    resource.amount *= capacityFactor;
                else
                    resource.amount = 0f;

                //Some templates don't apply the capaictyFactor
                //First, if we have no capacityFactorTypes entry, then apply the capacityFactor. This is for backwards compatibility.
                if (string.IsNullOrEmpty(capacityFactorTypes))
                    resource.maxAmount *= capacityFactor;

                //Next, if the capacityFactorTypes contains the template type then apply the capacity factor.
                else if (capacityFactorTypes.Contains(templateType))
                    resource.maxAmount *= capacityFactor;

                //If we aren't deployed then set the current and max amounts
                if (isDeployed == false && isInflatable)
                {
                    resource.maxAmount = 1.0f;
                    resource.amount = 0f;
                }

                _templateResources.Add(resource);
            }

            //Put back the resources that aren't already in the list
            foreach (PartResource savedResource in savedResources)
            {
                if (this.part.Resources.Contains(savedResource.resourceName) == false)
                {
                    this.part.Resources.list.Add(savedResource);
                    _templateResources.Add(resource);
                }
            }

            //The KAS resource is handled differently. We need to first find the KASModuleContainer
            kasContainer = this.part.Modules["KASModuleContainer"];
            if (kasContainer == null)
                return;

            //Ok, we have a KAS container. Now see if the template has an override for the KAS amount
            value = nodeTemplate.GetValue("kasAmount");
            if (string.IsNullOrEmpty(value) == false)
                kasAmount = (int)(float.Parse(value) * capacityFactor);

            //Use the default
            else
                kasAmount = _baseKasAmount;

            //If we are an inflatable module and inflated, set the KAS amount. Otherwise, set it to 1
            if (isInflatable && isDeployed == false)
                Utils.SetField("maxSize", 1, kasContainer);
            else
                Utils.SetField("maxSize", kasAmount, kasContainer);
        }

        protected void updateDecalsFromTemplate(ConfigNode nodeTemplate)
        {
            if (string.IsNullOrEmpty(_decalBasePath))
                return;

            Log("updateDecalsFromTemplate called");
            string value;

            value = nodeTemplate.GetValue("shortName");
            if (!string.IsNullOrEmpty(shortName))
            {
                //Set shortName
                shortName = value;
                Log("New shortName: " + shortName);

                //Logo panel
                if (parameterOverrides.ContainsKey(shortName))
                {
                    value = parameterOverrides[shortName].GetValue("logoPanel");

                    if (!string.IsNullOrEmpty(value))
                        logoPanelName = value;
                }
                else
                {
                    logoPanelName = nodeTemplate.GetValue("logoPanel");
                }

                //Glow panel
                if (parameterOverrides.ContainsKey(shortName))
                {
                    value = parameterOverrides[shortName].GetValue("glowPanel");

                    if (!string.IsNullOrEmpty(value))
                        glowPanelName = value;
                }
                else
                {
                    glowPanelName = nodeTemplate.GetValue("glowPanel");
                }

                //Change the decals
                changeDecals();
            }
            else
                Log("shortName is null");
        }

        protected void changeDecals()
        {
            Log("changeDecals called.");

            if (string.IsNullOrEmpty(_logoPanelTransforms))
            {
                Log("changeDecals has no named transforms to change.");
                return;
            }

            char[] delimiters = { ',' };
            string[] transformNames = _logoPanelTransforms.Replace(" ", "").Split(delimiters);
            Transform[] targets;
            Texture textureForDecal;
            Renderer rendererMaterial;

            //Sanity checks
            if (transformNames == null)
            {
                Log("transformNames are null");
                return;
            }
            if (string.IsNullOrEmpty(_decalBasePath))
            {
                Log("decalBasePath is null");
                return;
            }

            //Go through all the named panels and find their transforms.
            //Then replace their textures.
            foreach (string transformName in transformNames)
            {
                //Get the targets
                targets = part.FindModelTransforms(transformName);
                if (targets == null)
                {
                    Log("No targets found for " + transformName);
                    continue;
                }

                //Now, replace the textures in each target
                foreach (Transform target in targets)
                {
                    rendererMaterial = target.GetComponent<Renderer>();

                    textureForDecal = GameDatabase.Instance.GetTexture(_decalBasePath + "/" + logoPanelName, false);
                    if (textureForDecal != null)
                        rendererMaterial.material.SetTexture(MAIN_TEXTURE, textureForDecal);

                    textureForDecal = GameDatabase.Instance.GetTexture(_decalBasePath + "/" + glowPanelName, false);
                    if (textureForDecal != null)
                        rendererMaterial.material.SetTexture(EMISSIVE_TEXTURE, textureForDecal);
                }
            }
        }

        protected override void getProtoNodeValues(ConfigNode protoNode)
        {
            Log("getProtNodeValues called with node: " + protoNode.ToString());
            base.getProtoNodeValues(protoNode);

            ConfigNode[] overrideNodes = null;
            string value;

            //capacity factor
            if (capacityFactor == 0f)
            {
                value = protoNode.GetValue("capacityFactor");
                if (string.IsNullOrEmpty(value) == false)
                    capacityFactor = float.Parse(value);
            }

            value = protoNode.GetValue("fieldReconfigurable");
            if (string.IsNullOrEmpty(value) == false)
                fieldReconfigurable = bool.Parse(value);

            //Name of the nodes to use as templates
            _templateNodes = protoNode.GetValue("templateNodes");

            //Also get template types
            _templateTypes = protoNode.GetValue("templateTypes");

            //Set the defaults. We'll need them when we're in the editor
            //because the persistent KSP field seems to only apply to savegames.
            _defaultTemplate = protoNode.GetValue("defaultTemplate");

            //Get the list of resources that may be replaced when switching templates
            //If empty, then all of the part's resources will be cleared during a template switch.
            _resourcesToReplace = protoNode.GetValue("resourcesToReplace");

            //Location to the decals
            _decalBasePath = protoNode.GetValue("decalBasePath");

            value = protoNode.GetValue("confirmResourceSwitch");
            if (string.IsNullOrEmpty(value) == false)
                confirmResourceSwitch = bool.Parse(value);

            //Build dictionary of decal names & overrides
            overrideNodes = protoNode.GetNodes("OVERRIDE");
            foreach (ConfigNode decalNode in overrideNodes)
            {
                value = decalNode.GetValue("shortName");
                if (string.IsNullOrEmpty(value) == false)
                {
                    if (parameterOverrides.ContainsKey(value) == false)
                        parameterOverrides.Add(value, decalNode);
                }
            }

            //Get the list of transforms for the logo panels.
            if (_logoPanelTransforms == null)
                _logoPanelTransforms = protoNode.GetValue("logoPanelTransform");
        }

        protected virtual void hideEditorGUI(PartModule.StartState state)
        {
            Log("hideEditorGUI called");

            if (state == StartState.Editor)
            {
                this.Events["NextType"].guiActive = true;
                this.Events["PrevType"].guiActive = true;
            }

            else if (fieldReconfigurable == false)
            {
                this.Events["NextType"].guiActive = false;
                this.Events["PrevType"].guiActive = false;
            }
            else
            {
                this.Events["NextType"].guiActive = true;
                this.Events["PrevType"].guiActive = true;
            }
        }

        protected virtual void initModuleGUI()
        {
            Log("initModuleGUI called");
            int index;
            string value;

            //Change the toggle button's name
            index = templatesModel.GetNextUsableIndex(CurrentTemplateIndex);
            if (index != -1 && index != CurrentTemplateIndex)
            {
                value = templatesModel.templateNodes[index].GetValue("shortName");
                Events["NextType"].guiName = "Next: " + value;
            }

            index = templatesModel.GetPrevUsableIndex(CurrentTemplateIndex);
            if (index != -1 && index != CurrentTemplateIndex)
            {
                value = templatesModel.templateNodes[index].GetValue("shortName");
                Events["PrevType"].guiName = "Prev: " + value;
            }
        }
        
        protected void initTemplates()
        {
            Log("initTemplates called with templateNodes: " + _templateNodes + " templateTypes: " + _templateTypes);
            //Create templates object if needed.
            //This can happen when the object is cloned in the editor (On Load won't be called).
            if (templatesModel == null)
                templatesModel = new TemplatesModel(this.part, this.vessel, new LogDelegate(Log));
            templatesModel.templateNodeName = _templateNodes;
            templatesModel.templateTypes = _templateTypes;

            if (templatesModel.templateNodes == null)
            {
                Log("OnStart templateNodes == null!");
                return;
            }

            //Set default template if needed
            //This will happen when we're in the editor.
            if (string.IsNullOrEmpty(shortName))
                shortName = _defaultTemplate;

            //Set current template index
            CurrentTemplateIndex = templatesModel.FindIndexOfTemplate(shortName);
        }
        #endregion

    }
}
