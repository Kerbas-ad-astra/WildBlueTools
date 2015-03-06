using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;

/*
Source code copyrighgt 2014, by Michael Billard (Angel-125)
License: CC BY-NC-SA 4.0
License URL: https://creativecommons.org/licenses/by-nc-sa/4.0/
Wild Blue Industries is trademarked by Michael Billard and may be used for non-commercial purposes. All other rights reserved.
Note that Wild Blue Industries is a ficticious entity 
created for entertainment purposes. It is in no way meant to represent a real entity.
Any similarity to a real entity is purely coincidental.

Portions of this software use code from the Firespitter plugin by Snjo, used with permission. Thanks Snjo for sharing how to switch meshes. :)

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
namespace WildBlueIndustries
{
    public class WBIMeshHelper : ExtendedPartModule
    {
        [KSPField]
        public string objects = string.Empty;

        [KSPField(isPersistant = true)]
        public int selectedObject = 0;

        protected List<List<Transform>> objectTransforms = new List<List<Transform>>();
        protected Dictionary<string, int> meshIndexes = new Dictionary<string, int>();
        protected bool showGui = false;

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Next variant", active = true)]
        public void NextMesh()
        {
            int nextIndex = selectedObject;

            nextIndex = (nextIndex + 1) % this.objectTransforms.Count;

            setObject(nextIndex);
        }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Prev variant", active = true)]
        public void PrevMesh()
        {
            int nextIndex = selectedObject;

            nextIndex = (nextIndex - 1) % this.objectTransforms.Count;

            setObject(nextIndex);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            node.AddValue("selectedObject", selectedObject.ToString());

            node.AddValue("showGui", showGui.ToString());
        }

        public virtual void OnEditorAttach()
        {
            Events["NextMesh"].active = showGui;
            Events["NextMesh"].guiActive = showGui;
            Events["NextMesh"].guiActiveEditor = showGui;
            Events["PrevMesh"].active = showGui;
            Events["PrevMesh"].guiActive = showGui;
            Events["PrevMesh"].guiActiveEditor = showGui;
        }

        protected override void getProtoNodeValues(ConfigNode protoNode)
        {
            base.getProtoNodeValues(protoNode);

            string value = protoNode.GetValue("selectedObject");
            if (string.IsNullOrEmpty(value) == false)
                selectedObject = int.Parse(value);

            value = protoNode.GetValue("showGui");
            if (string.IsNullOrEmpty(value) == false)
                showGui = bool.Parse(value);
        }

        public override void OnStart(StartState state)
        {
            parseObjectNames();
            setObject(selectedObject);
            base.OnStart(state);
            string[] meshes;

            this.part.OnEditorAttach += OnEditorAttach;

            Events["NextMesh"].active = showGui;
            Events["NextMesh"].guiActive = showGui;
            Events["NextMesh"].guiActiveEditor = showGui;
            Events["PrevMesh"].active = showGui;
            Events["PrevMesh"].guiActive = showGui;
            Events["PrevMesh"].guiActiveEditor = showGui;

            //Go through each entry and split up the entry into its template name and mesh index
            meshes = objects.Split(';');
            for (int index = 0; index < meshes.Count<string>(); index++)
                meshIndexes.Add(meshes[index], index);
        }

        protected void parseObjectNames()
        {
            string[] objectBatchNames = objects.Split(';');
            if (objectBatchNames.Length >= 1)
            {
                objectTransforms.Clear();
                for (int batchCount = 0; batchCount < objectBatchNames.Length; batchCount++)
                {
                    List<Transform> newObjects = new List<Transform>();
                    string[] objectNames = objectBatchNames[batchCount].Split(',');
                    for (int objectCount = 0; objectCount < objectNames.Length; objectCount++)
                    {
                        Transform newTransform = part.FindModelTransform(objectNames[objectCount].Trim(' '));
                        if (newTransform != null)
                        {
                            newObjects.Add(newTransform);
                            Log("Added object to list: " + objectNames[objectCount]);
                        }
                        else
                        {
                            Log("Could not find object " + objectNames[objectCount]);
                        }
                    }
                    if (newObjects.Count > 0) objectTransforms.Add(newObjects);
                }
            }
        }

        protected void setObject(int objectNumber, bool startHidden = true)
        {
            if (startHidden)
            {
                for (int i = 0; i < objectTransforms.Count; i++)
                {
                    for (int j = 0; j < objectTransforms[i].Count; j++)
                    {
                        Log("Setting object enabled");
                        objectTransforms[i][j].gameObject.SetActive(false);

                        Log("setting collider states");
                        if (objectTransforms[i][j].gameObject.collider != null)
                            objectTransforms[i][j].gameObject.collider.enabled = false;
                    }
                }
            }

            //If we have no object selected then just exit.
            if (objectNumber == -1)
                return;

            // enable the selected one last because there might be several entries with the same object, and we don't want to disable it after it's been enabled.
            for (int i = 0; i < objectTransforms[objectNumber].Count; i++)
            {
                objectTransforms[objectNumber][i].gameObject.SetActive(true);

                if (objectTransforms[objectNumber][i].gameObject.collider != null)
                {
                    Log("Setting collider true on new active object");
                    objectTransforms[objectNumber][i].gameObject.collider.enabled = true;
                }
            }

            selectedObject = objectNumber;
        }

        protected void setObjects(List<int> objects)
        {
            setObject(-1);

            foreach (int objectId in objects)
                setObject(objectId, false);
        }

    }
}
