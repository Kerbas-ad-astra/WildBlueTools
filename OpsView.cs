using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;

/*
Source code copyrighgt 2015, by Michael Billard (Angel-125)
License: CC BY-NC-SA 4.0
License URL: https://creativecommons.org/licenses/by-nc-sa/4.0/
If you want to use this code, give me a shout on the KSP forums! :)
Wild Blue Industries is trademarked by Michael Billard and may be used for non-commercial purposes. All other rights reserved.
Note that Wild Blue Industries is a ficticious entity 
created for entertainment purposes. It is in no way meant to represent a real entity.
Any similarity to a real entity is purely coincidental.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
namespace WildBlueIndustries
{
    public delegate bool TemplateHasOpsWindow();
    public delegate void DrawTemplateOps();
    public delegate void NextModule();
    public delegate void PrevModule();
    public delegate void NextPreviewModule(string templateName);
    public delegate void PrevPreviewModule(string templateName);
    public delegate void ChangeModuleType(string templateName);
    public delegate string GetModuleInfo(string templateName);
    public delegate Texture GetModuleLogo(string templateName);

    public class OpsView : Window<OpsView>
    {
        public List<ModuleResourceConverter> converters = null;
        public Part part;
        public PartResourceList resources;
        public bool techResearched;
        public string nextName;
        public string prevName;
        public string previewName;
        public string cost;
        public NextModule nextModuleDelegate = null;
        public PrevModule prevModuleDelegate = null;
        public NextPreviewModule nextPreviewDelegate = null;
        public PrevPreviewModule prevPreviewDelegate = null;
        public ChangeModuleType changeModuleTypeDelegate = null;
        public GetModuleInfo getModuleInfoDelegate = null;
        public GetModuleLogo getModuleLogoDelegate = null;
        public TemplateHasOpsWindow teplateHasOpsWindowDelegate = null;
        public DrawTemplateOps drawTemplateOpsDelegate = null;

        private Vector2 _scrollPosConverters;
        private Vector2 _scrollPosResources;
        private InfoView modSummary = new InfoView();
        private string moduleInfo;
        ModuleCommand commandModule;
        WBIResourceSwitcher switcher;
        WBILight lightModule;
        protected bool drawTemplateOps;
        string[] tabs = new string[] { "Info", "Resources" };
        int selectedTab = 0;
        private string[] managementTabs = new string[] { "Processors", "Command & Control" };
        private int managementTab = 0;
        private string _shortName;
        public Texture moduleLabel;

        public OpsView() :
        base("Operations Manager", 600, 330)
        {
            Resizable = false;
            _scrollPosConverters = new Vector2(0, 0);
            _scrollPosResources = new Vector2(0, 0);
        }

        public string shortName
        {
            get
            {
                return _shortName;
            }

            set
            {
                _shortName = value;
                moduleInfo = getModuleInfoDelegate(_shortName);
                moduleLabel = getModuleLogoDelegate(shortName);
            }
        }

        public override void OnGUI()
        {
            base.OnGUI();
            if (modSummary.IsVisible())
                modSummary.OnGUI();
        }

        public void GetPartModules()
        {
            commandModule = this.part.FindModuleImplementing<ModuleCommand>();
            if (commandModule != null)
                foreach (BaseEvent cmdEvent in commandModule.Events)
                {
                    cmdEvent.guiActive = false;
                    cmdEvent.guiActiveUnfocused = false;
                }

            switcher = this.part.FindModuleImplementing<WBIResourceSwitcher>();
            if (switcher != null)
            {
                switcher.Events["ToggleDecals"].guiActive = false;
                switcher.Events["ToggleDecals"].guiActiveUnfocused = false;
            }

            lightModule = this.part.FindModuleImplementing<WBILight>();
            if (lightModule != null)
                lightModule.showGui(false);

        }

        protected override void DrawWindowContents(int windowId)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();

            GUILayout.Label("Current: " + shortName);

            if (teplateHasOpsWindowDelegate != null)
            {
                bool hasOpsWindow = teplateHasOpsWindowDelegate();
                if (hasOpsWindow && drawTemplateOpsDelegate != null)
                {
                    string buttonTitle = drawTemplateOps == true ? "Hide" : "Show";

                    if (GUILayout.Button(buttonTitle, GUILayout.Width(50)))
                        drawTemplateOps = !drawTemplateOps;

                    if (drawTemplateOps)
                    {
                        GUILayout.EndHorizontal();
                        drawTemplateOpsDelegate();
                        GUILayout.EndVertical();
                        return;
                    }
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            //Left pane : Module management controls
            drawModuleManagementPane();

            //Right pane: info/resource pane
            GUILayout.BeginVertical();
            if (!HighLogic.LoadedSceneIsEditor)
            {
                selectedTab = GUILayout.SelectionGrid(selectedTab, tabs, tabs.Length);
                if (selectedTab == 0)
                    drawInfoPane();
                else
                    drawResourcePane();
            }

            else
            {
                drawInfoPane();
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        protected void drawInfoPane()
        {
            _scrollPosResources = GUILayout.BeginScrollView(_scrollPosResources, new GUIStyle(GUI.skin.textArea));
            if (moduleLabel != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(moduleLabel, new GUILayoutOption[] { GUILayout.Width(128), GUILayout.Height(128) });
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            GUILayout.Label(moduleInfo, new GUILayoutOption[] { GUILayout.Width(190)});
            GUILayout.EndScrollView();
        }

        protected void drawResourcePane()
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Resources");

            _scrollPosResources = GUILayout.BeginScrollView(_scrollPosResources, new GUIStyle(GUI.skin.textArea));
            foreach (PartResource resource in this.part.Resources)
            {
                GUILayout.Label(resource.resourceName);
                GUILayout.Label(String.Format("{0:#,##0.00}/{1:#,##0.00}", resource.amount, resource.maxAmount));
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        protected virtual void drawModuleManagementPane()
        {
            GUILayout.BeginVertical(GUILayout.MaxWidth(350f));
            GUILayout.Space(4);

            managementTab = GUILayout.SelectionGrid(managementTab, managementTabs, tabs.Length);
            if (managementTab == 0)
            {
                //Draw converters
                drawConverters();

                //Depending upon loaded scene, we'll either show the module next/prev buttons and labels
                //or we'll show the module preview buttons.
                if (!HighLogic.LoadedSceneIsEditor)
                    drawPreviewGUI();
                else
                    drawEditorGUI();
            }

            else //C&C tab
            {
                //Control From Here
                if (commandModule != null)
                {
                    if (GUILayout.Button("Control From Here"))
                        commandModule.MakeReference();

                    //Rename Vessel
                    if (GUILayout.Button("Rename Base"))
                        commandModule.RenameVessel();
                }

                //Toggle Decals
                if (switcher != null)
                    if (GUILayout.Button("Toggle Decals"))
                        switcher.ToggleDecals();

                //Toggle Lights
                if (lightModule != null)
                    if (GUILayout.Button("Toggle Lights"))
                        lightModule.ToggleAnimation();
            }

            GUILayout.EndVertical();
        }

        protected void drawEditorGUI()
        {
            //Next/Prev buttons
            if (GUILayout.Button("Next: " + nextName))
                if (nextModuleDelegate != null)
                    nextModuleDelegate();

            if (GUILayout.Button("Prev: " + prevName))
                if (prevModuleDelegate != null)
                    prevModuleDelegate();
        }

        protected void drawPreviewGUI()
        {
            //Only allow reconfiguring of the module if it allows field reconfiguration.
            if (techResearched == false)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("This module cannot be reconfigured. Research more technology.");
                GUILayout.FlexibleSpace();
                return;
            }

            string moduleInfo;

            GUILayout.Label("Current Preview: " + previewName);
            GUILayout.Label("Reconfiguration Cost: " + cost + " RocketParts");

            //Make sure we have something to display
            if (string.IsNullOrEmpty(previewName))
                previewName = nextName;

            if (converters.Count > 2)
            {
                //Next preview button
                if (GUILayout.Button("Next: " + nextName))
                {
                    if (nextPreviewDelegate != null)
                        nextPreviewDelegate(previewName);
                }

                //Prev preview button
                if (GUILayout.Button("Prev: " + prevName))
                {
                    if (prevPreviewDelegate != null)
                        prevPreviewDelegate(previewName);
                }
            }

            else
            {
                //Next preview button
                if (GUILayout.Button("Next: " + nextName))
                {
                    if (nextPreviewDelegate != null)
                        nextPreviewDelegate(previewName);
                }
            }

            //More info button
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("More Info"))
            {
                if (getModuleInfoDelegate != null)
                {
                    moduleInfo = getModuleInfoDelegate(previewName);
                    InfoView modSummary = new InfoView();
                    Texture moduleLabel;

                    modSummary.ModuleInfo = moduleInfo;

                    if (this.getModuleLogoDelegate != null)
                    {
                        moduleLabel = getModuleLogoDelegate(previewName);
                        modSummary.moduleLabel = moduleLabel;
                    }
                    modSummary.ToggleVisible();

                }
            }

            if (GUILayout.Button("Reconfigure"))
            {
                if (nextName == shortName)
                    ScreenMessages.PostScreenMessage("No need to redecorate to the same module type.", 5.0f, ScreenMessageStyle.UPPER_CENTER);

                else if (changeModuleTypeDelegate != null)
                    changeModuleTypeDelegate(previewName);
            }
            GUILayout.EndHorizontal();
        }

        protected void drawConverters()
        {
            GUILayout.BeginVertical(GUILayout.MinHeight(110));
            string converterName = "??";
            string converterStatus = "??";
            bool isActivated;

            _scrollPosConverters = GUILayout.BeginScrollView(_scrollPosConverters, new GUIStyle(GUI.skin.textArea));

            foreach (ModuleResourceConverter converter in converters)
            {
                converterName = converter.ConverterName;
                converterStatus = converter.status;
                isActivated = converter.IsActivated;

                GUILayout.BeginVertical();

                //Toggle, name and status message
                if (!HighLogic.LoadedSceneIsEditor)
                    isActivated = GUILayout.Toggle(isActivated, converterName + ": " + converterStatus);
                else
                    isActivated = GUILayout.Toggle(isActivated, converterName);

                if (converter.IsActivated != isActivated)
                {
                    if (isActivated)
                        converter.StartResourceConverter();
                    else
                        converter.StopResourceConverter();
                }

                GUILayout.EndVertical();
            }

            if (converters.Count == 0)
                GUILayout.Label("No processors are present in this configuration.");

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }
    }
}