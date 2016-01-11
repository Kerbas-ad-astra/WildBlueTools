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
    public class ButtonClickWatcher : MonoBehaviour
    {
        protected bool mouseDown;

        public void OnMouseDown()
        {
            mouseDown = true;
            Debug.Log("FRED OnMouseDown");
        }

        public void OnMouseUp()
        {
            mouseDown = false;
            Debug.Log("FRED OnMouseUp");
        }
    }

    public class InternalScreenSwitcher : InternalModule
    {
        private static string MAIN_TEXTURE = "_MainTex";
        private static string EMISSIVE_TEXTURE = "_Emissive";

        [KSPField]
        public string screenTransform = "Screen";

        public void Start()
        {
            if (HighLogic.LoadedSceneIsFlight == false)
                return;

            Transform trans = internalProp.FindModelTransform("ScreenTrigger");
            if (trans != null)
            {
                GameObject goButton = trans.gameObject;
                if (goButton != null)
                {
                    ButtonClickWatcher clickWatcher = goButton.GetComponent<ButtonClickWatcher>();
                    if (clickWatcher == null)
                    {
                        clickWatcher = goButton.AddComponent<ButtonClickWatcher>();
                    }
                }
            }
        }

        public void ChangeTexture(Texture2D newTexture)
        {
            Transform[] targets;
            Renderer rendererMaterial;

            //Get the targets
            targets = internalProp.FindModelTransforms(screenTransform);
            if (targets == null)
            {
                Debug.Log("No targets found for " + screenTransform);
                return;
            }

            //Now, replace the textures in each target
            foreach (Transform target in targets)
            {
                rendererMaterial = target.GetComponent<Renderer>();
                rendererMaterial.material.SetTexture(MAIN_TEXTURE, newTexture);
                rendererMaterial.material.SetTexture(EMISSIVE_TEXTURE, newTexture);
            }
        }
    }

}
