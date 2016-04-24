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
    public delegate void PreviewNextStorage(string templateName);
    public delegate void PreviewPrevStorage(string templateName);
    public delegate void SetTemplate(string template);

    public class ConvertibleStorageView : Window<ConvertibleStorageView>
    {
        public string info;
        public Texture decal;
        public string requiredResource = string.Empty;
        public float resourceCost = 100f;
        public string templateName;
        public int templateCount;
        public string requiredSkill = string.Empty;

        public PreviewNextStorage previewNext;
        public PreviewPrevStorage previewPrev;
        public SetTemplate setTemplate;

        private Vector2 _scrollPos;
        private bool confirmReconfigure;

        public ConvertibleStorageView() :
        base("Configure Storage", 640, 480)
        {
            Resizable = false;
            _scrollPos = new Vector2(0, 0);
        }

        public override void SetVisible(bool newValue)
        {
            base.SetVisible(newValue);
        }

        protected override void DrawWindowContents(int windowId)
        {
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();

            GUILayout.Label("Configuration: " + templateName);

            if (string.IsNullOrEmpty(requiredResource) == false && resourceCost != 0f)
                GUILayout.Label(string.Format("Cost: {0:s} ({1:f2})", requiredResource, resourceCost));
            else
                GUILayout.Label("Cost: NONE");

            if (string.IsNullOrEmpty(requiredSkill) == false)
                GUILayout.Label("Reconfigure Skill: " + requiredSkill);
            else
                GUILayout.Label("Reconfigure Skill: NONE");

            if (templateCount > 1)
            {
                if (GUILayout.Button("Next") && previewNext != null)
                    previewNext(templateName);
            }

            if (templateCount >= 4)
            {
                if (GUILayout.Button("Prev") && previewPrev != null)
                {
                    previewPrev(templateName);
                }
            }

            if (GUILayout.Button("Reconfigure") && setTemplate != null)
            {
                if (confirmReconfigure)
                {
                    setTemplate(templateName);
                    confirmReconfigure = false;
                }

                else
                {
                    ScreenMessages.PostScreenMessage("Click again to confirm reconfiguration.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    confirmReconfigure = true;
                }
            }

            GUILayout.EndVertical();

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, new GUILayoutOption[] {GUILayout.Width(350)});

            if (decal != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(decal, new GUILayoutOption[] { GUILayout.Width(128), GUILayout.Height(128) });
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.Label(info);

            GUILayout.EndScrollView();

            GUILayout.EndHorizontal();
        }
    }
}
