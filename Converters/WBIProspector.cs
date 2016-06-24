using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;

/*
Source code copyrighgt 2015 - 2016, by Michael Billard (Angel-125)
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
    public class WBIProspector : ModuleResourceConverter, IOpsView
    {
        [KSPField]
        public string inputResource = string.Empty;

        [KSPField]
        public float inputRatio;

        [KSPField]
        public string byproduct = string.Empty;

        [KSPField]
        public float byproductMinPercent;

        [KSPField]
        public string ignoreResources = string.Empty;

        protected float inputMass;
        protected float byproductMass;
        protected float yieldMass;
        protected PartResourceDefinition inputDef = null;
        protected PartResourceDefinition byproductDef = null;

        public override void OnStart(StartState state)
        {
            if (!string.IsNullOrEmpty(inputResource))
            {
                ResourceRatio inputSource = new ResourceRatio { ResourceName = inputResource, Ratio = inputRatio, FlowMode = "ALL_VESSEL" };
                inputList.Add(inputSource);
            }

            if (HighLogic.LoadedSceneIsFlight)
                prepareOutputs();

            base.OnStart(state);
        }

        public override string GetInfo()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                //Add the input source if needed.
                if (!string.IsNullOrEmpty(inputResource))
                {
                    ResourceRatio inputSource = new ResourceRatio { ResourceName = inputResource, Ratio = inputRatio, FlowMode = "ALL_VESSEL" };
                    inputList.Add(inputSource);
                }

                if (outputList.Count == 0)
                    prepareOutputs();

                return base.GetInfo();
            }
            else
            {
                return base.GetInfo() + "\r\nOutput varies depending upon location.";
            }
        }

        protected void prepareOutputs()
        {
            if (string.IsNullOrEmpty(inputResource))
                return;
            if (string.IsNullOrEmpty(byproduct))
                return;

            inputDef = ResourceHelper.DefinitionForResource(inputResource);
            byproductDef = ResourceHelper.DefinitionForResource(byproduct);
            inputMass = inputDef.density * inputRatio;
            byproductMass = inputMass * (byproductMinPercent / 100.0f);
            yieldMass = inputMass - byproductMass;

            Debug.Log("FRED inputMass: " + inputMass);
            Debug.Log("FRED byproductMass: " + byproductMass);
            Debug.Log("FRED yieldMass: " + yieldMass);

            prepareOutputsByLocale();
        }

        protected virtual void prepareOutputsByLocale()
        {
            ResourceRatio outputSource = null;
            string biomeName = Utils.GetCurrentBiome(this.part.vessel).name;
            PartResourceDefinition outputDef = null;
            float totalAbundance = 0f;
            float abundance = 0f;
            float outputMass = 0f;
            float outputUnits = 0f;
            IEnumerable<ResourceCache.AbundanceSummary> abundanceCache = ResourceCache.Instance.AbundanceCache.
                Where(a => a.HarvestType == HarvestTypes.Planetary && a.BodyId == this.part.vessel.mainBody.flightGlobalsIndex && a.BiomeName == biomeName);

            foreach (ResourceCache.AbundanceSummary summary in abundanceCache)
            {
                outputDef = ResourceHelper.DefinitionForResource(summary.ResourceName);
                abundance = summary.Abundance;
                outputMass = abundance * yieldMass;
                outputUnits = outputMass / outputDef.density;

                //If the resource is our input resource then add the output mass to the byproductMass.
                if (summary.ResourceName == inputResource)
                {
                    byproductMass += outputMass;
                }
                else if (!string.IsNullOrEmpty(ignoreResources) && ignoreResources.Contains(summary.ResourceName))
                {
                    byproductMass += outputMass;
                }
                else
                {
                    totalAbundance += abundance;
                    Debug.Log("FRED " + summary.ResourceName + " abundance: " + abundance + " Ratio: " + outputUnits);
                    outputSource = new ResourceRatio { ResourceName = summary.ResourceName, Ratio = outputUnits, FlowMode = "ALL_VESSEL", DumpExcess = true };
                    outputList.Add(outputSource);
                }
            }

            //Leftovers
            byproductMass += (1.0f - totalAbundance) * yieldMass;
            outputUnits = byproductMass / byproductDef.density;
            outputSource = new ResourceRatio { ResourceName = byproduct, Ratio = outputUnits, FlowMode = "ALL_VESSEL", DumpExcess = true };
            outputList.Add(outputSource);

            Debug.Log("FRED totalAbundance: " + totalAbundance);
            Debug.Log("FRED Slag Units: " + outputUnits);
        }

        public List<string> GetButtonLabels()
        {
            List<string> buttonLabels = new List<string>();
            buttonLabels.Add("Prospector");
            return buttonLabels;
        }

        public void DrawOpsWindow(string buttonLabel)
        {
            GUILayout.Label("Nothing to see here");
        }

        public void SetParentView(IParentView parentView)
        {
        }

        public void SetContextGUIVisible(bool isVisible)
        {
            foreach (BaseEvent baseEvent in this.Events)
            {
                baseEvent.guiActive = isVisible;
                baseEvent.guiActiveEditor = isVisible;
            }

            foreach (BaseField baseField in this.Fields)
            {
                baseField.guiActive = isVisible;
                baseField.guiActiveEditor = isVisible;
            }

            //Dirty the GUI
            UIPartActionWindow tweakableUI = Utils.FindActionWindow(this.part);
            if (tweakableUI != null)
                tweakableUI.displayDirty = true;
        }

        public string GetPartTitle()
        {
            throw new NotImplementedException();
        }
    }
}
