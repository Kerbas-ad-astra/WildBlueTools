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
    public struct SDrawbleView
    {
        public string buttonLabel;
        public IOpsView view;
    }

    public class OpsManagerView : Window<OpsManagerView>, IOpsView, IParentView
    {
        public bool hasDecals;
        public Part part;
        public ConvertibleStorageView storageView;
        public List<ModuleResourceConverter> converters = new List<ModuleResourceConverter>();

        private Vector2 _scrollPosViews, _scrollPosResources, _scrollPosConverters;
        List<SDrawbleView> views = new List<SDrawbleView>();
        SDrawbleView currentDrawableView;
        ModuleCommand commandModule;
        WBIResourceSwitcher switcher;
        WBILight lightModule;

        public OpsManagerView() :
        base("Manage Operations", 800, 480)
        {
            Resizable = false;
        }

        #region IParentView
        public void SetParentVisible(bool isVisible)
        {
            SetVisible(isVisible);
        }
        #endregion

        public override void SetVisible(bool newValue)
        {
            base.SetVisible(newValue);

            if (newValue)
            {
                UpdateButtonTabs();

                //Set initial view
                currentDrawableView = views[0];
            }
        }

        public void UpdateConverters()
        {
            converters.Clear();
            List<ModuleResourceConverter> possibleConverters = this.part.FindModulesImplementing<ModuleResourceConverter>();

            //Now get rid of anything that is a basic science lab
            foreach (ModuleResourceConverter converter in possibleConverters)
            {
                if (!(converter is WBIBasicScienceLab))
                    converters.Add(converter);
            }
        }

        public void UpdateButtonTabs()
        {
            //Get our part modules
            UpdateConverters();
            getPartModules();

            views.Clear();

            //Built-in views: Config
            SDrawbleView drawableView = new SDrawbleView();
            drawableView.buttonLabel = "Config";
            drawableView.view = this;
            views.Add(drawableView);

            //Command
            if (hasDecals || commandModule != null || lightModule != null)
            {
                drawableView = new SDrawbleView();
                drawableView.buttonLabel = "Command";
                drawableView.view = this;
                views.Add(drawableView);
            }

            //Resources
            drawableView = new SDrawbleView();
            drawableView.buttonLabel = "Resources";
            drawableView.view = this;
            views.Add(drawableView);

            //Converters
            drawableView = new SDrawbleView();
            drawableView.buttonLabel = "Converters";
            drawableView.view = this;
            views.Add(drawableView);

            //Custom views from other PartModules
            List<IOpsView> templateOpsViews = this.part.FindModulesImplementing<IOpsView>();
            foreach (IOpsView templateOps in templateOpsViews)
            {
                if (templateOps != (IOpsView)this)
                {
                    List<string> labels = templateOps.GetButtonLabels();
                    foreach (string label in labels)
                    {
                        drawableView = new SDrawbleView();
                        drawableView.buttonLabel = label;
                        drawableView.view = templateOps;
                        templateOps.SetParentView(this);
                        templateOps.SetContextGUIVisible(false);
                        views.Add(drawableView);
                    }
                }
            }
        }

        #region IOpsView
        public void SetParentView(IParentView parentView)
        {
        }

        public void DrawOpsWindow(string buttonLabel)
        {
            switch (currentDrawableView.buttonLabel)
            {
                case "Config":
                    storageView.DrawView();
                    break;

                case "Command":
                    drawCommandView();
                    break;

                case "Resources":
                    drawResourceView();
                    break;

                case "Converters":
                    drawConvertersView();
                    break;

                default:
                    currentDrawableView.view.DrawOpsWindow(buttonLabel);
                    break;
            }
        }

        public List<string> GetButtonLabels()
        {
            List<string> buttonLabels = new List<string>();

            //Get our part modules
            UpdateConverters();
            getPartModules();

            buttonLabels.Add("Config");
            buttonLabels.Add("Command");
            buttonLabels.Add("Resources");
            buttonLabels.Add("Converters");

            return buttonLabels;
        }

        public void SetContextGUIVisible(bool isVisible)
        {
        }

        #endregion

        protected override void DrawWindowContents(int windowId)
        {
            GUILayout.BeginHorizontal();

            //View buttons
            _scrollPosViews = GUILayout.BeginScrollView(_scrollPosViews, new GUILayoutOption[] { GUILayout.Width(160) });
            foreach (SDrawbleView drawableView in views)
            {
                if (GUILayout.Button(drawableView.buttonLabel))
                {
                    currentDrawableView = drawableView;
                }
            }
            GUILayout.EndScrollView();

            //CurrentView
            currentDrawableView.view.DrawOpsWindow(currentDrawableView.buttonLabel);

            GUILayout.EndHorizontal();
        }

        public void getPartModules()
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
                switcher.Events["DumpResources"].guiActive = false;
                switcher.Events["DumpResources"].guiActiveUnfocused = false;
            }

            lightModule = this.part.FindModuleImplementing<WBILight>();
            if (lightModule != null)
                lightModule.showGui(false);

        }

        protected void drawCommandView()
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Command & Control");

            if (!HighLogic.LoadedSceneIsFlight)
            {
                GUILayout.Label("This configuration is working, but the contents can only be accessed in flight.");
                GUILayout.EndVertical();
                return;
            }

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
            if (switcher != null && switcher.decalsVisible)
            {
                if (GUILayout.Button("Toggle Decals"))
                    switcher.ToggleDecals();
            }

            //Dump Resources
            if (switcher != null)
            {
                if (GUILayout.Button("Dump Resources"))
                    switcher.DumpResources();
            }

            //Toggle Lights
            if (lightModule != null)
            {
                if (GUILayout.Button("Toggle Lights"))
                    lightModule.ToggleAnimation();
            }

            GUILayout.EndVertical();
        }

        protected void drawResourceView()
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Resources");

            if (this.part.Resources.Count == 0)
            {
                GUILayout.Label("<color=yellow>This configuration does not have any resources in it.</color>");
                GUILayout.EndVertical();
                return;
            }

            _scrollPosResources = GUILayout.BeginScrollView(_scrollPosResources, new GUIStyle(GUI.skin.textArea));
            foreach (PartResource resource in this.part.Resources)
            {
                if (resource.isVisible)
                {
                    GUILayout.Label(resource.resourceName);
                    GUILayout.Label(String.Format("<color=white>{0:#,##0.00}/{1:#,##0.00}</color>", resource.amount, resource.maxAmount));
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        protected void drawConvertersView()
        {
            GUILayout.BeginVertical();
            string converterName = "??";
            string converterStatus = "??";
            bool isActivated;

            GUILayout.Label("Converters");

            if (converters.Count == 0)
            {
                GUILayout.Label("<color=yellow>This configuration does not have any resource converters in it.</color>");
                GUILayout.EndVertical();
                return;
            }

            _scrollPosConverters = GUILayout.BeginScrollView(_scrollPosConverters, new GUIStyle(GUI.skin.textArea));

            foreach (ModuleResourceConverter converter in converters)
            {
                converterName = converter.ConverterName;
                converterStatus = converter.status;
                isActivated = converter.IsActivated;

                GUILayout.BeginVertical();

                //Toggle, name and status message
                if (!HighLogic.LoadedSceneIsEditor)
                    isActivated = GUILayout.Toggle(isActivated, string.Format(converterName + " ({0:f1}%): ", converter.Efficiency * converter.EfficiencyBonus * 100f) + converterStatus);
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

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

    }
}
