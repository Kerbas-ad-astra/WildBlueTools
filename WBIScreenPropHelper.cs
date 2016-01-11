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
    public struct ScreenInfo
    {
        public string name;
        public Texture2D texture;
        public int index;
        public bool locked;
    }

    class WBIScreenPropHelper : ExtendedPartModule
    {
        [KSPField(isPersistant = true)]
        public bool moduleEnabled = true;

        [KSPField]
        public float screenChangeSeconds = 0f;

        [KSPField]
        public string screenImagePaths = "";

        protected List<InternalScreenSwitcher> screenSwitchers = new List<InternalScreenSwitcher>();
        protected List<ScreenInfo> screenImages = new List<ScreenInfo>();
        protected Dictionary<int, ScreenInfo> startingScreenImages = new Dictionary<int, ScreenInfo>();

        double cycleStartTime;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (HighLogic.LoadedSceneIsFlight == false)
                return;

            //Look for internal screens
            foreach (InternalProp prop in this.part.internalModel.props)
            {
                foreach (InternalModule intModule in prop.internalModules)
                {
                    if (intModule.ClassName == "InternalScreenSwitcher")
                        screenSwitchers.Add((InternalScreenSwitcher)intModule);
                }
            }

            //Get the screens that we'll use
            loadScreenImages();

            //Setup the initial screen images
            setupInitialImages();

            //Create first set of random images
            randomizeScreenImages();

            //If we have no screens then we're done, don't accept update calls
            if (screenSwitchers.Count == 0 || moduleEnabled == false)
            {
                this.enabled = false;
                this.isEnabled = false;
                return;
            }

            //Get the current time
            cycleStartTime = Planetarium.GetUniversalTime();
        }

        public override void OnFixedUpdate()
        {
            base.OnFixedUpdate();

            double elapsedTime = Planetarium.GetUniversalTime() - cycleStartTime;

            //See if we should change the screens
            if (elapsedTime >= screenChangeSeconds)
            {
                //Reset the timer
                cycleStartTime = Planetarium.GetUniversalTime();

                //Change screen images
                randomizeScreenImages();
            }
        }

        protected override void getProtoNodeValues(ConfigNode protoNode)
        {
            base.getProtoNodeValues(protoNode);

            if (protoNode.HasValue("moduleEnabled"))
                moduleEnabled = bool.Parse(protoNode.GetValue("moduleEnabled"));

            if (protoNode.HasValue("screenImagePaths"))
                screenImagePaths = protoNode.GetValue("screenImagePaths");

            if (protoNode.HasValue("screenChangeSeconds"))
                screenChangeSeconds = float.Parse(protoNode.GetValue("screenChangeSeconds"));

            if (protoNode.HasNode("SCREEN"))
            {
                ConfigNode[] screenNodes = protoNode.GetNodes("SCREEN");
                foreach (ConfigNode node in screenNodes)
                {
                    ScreenInfo screenInfo = new ScreenInfo();

                    if (node.HasValue("index"))
                        screenInfo.index = int.Parse(node.GetValue("index"));
                    if (node.HasValue("image"))
                        screenInfo.name = node.GetValue("image");
                    if (node.HasValue("isLocked"))
                        screenInfo.locked = bool.Parse(node.GetValue("isLocked"));
                    screenInfo.texture = GameDatabase.Instance.GetTexture(screenInfo.name, false);

                    startingScreenImages.Add(screenInfo.index, screenInfo);
                }
            }
        }

        protected void loadScreenImages()
        {
            if (string.IsNullOrEmpty(screenImagePaths))
            {
                Debug.Log("No screens to load.");
                return;
            }
            string[] imagePaths = screenImagePaths.Split(new char[] {';'});

            //Load the images
            foreach (string imagePath in imagePaths)
            {
                List<GameDatabase.TextureInfo> textureInfos = GameDatabase.Instance.GetAllTexturesInFolder(imagePath);
                foreach (GameDatabase.TextureInfo textureInfo in textureInfos)
                {
                    ScreenInfo screenInfo = new ScreenInfo();
                    screenInfo.name = textureInfo.name;
                    screenInfo.texture = textureInfo.texture;
                    screenImages.Add(screenInfo);
                }
            }

            //Make sure we have images
            if (screenImages.Count == 0)
            {
                Debug.Log("No screens to load.");
                this.enabled = false;
                this.isEnabled = false;
            }
        }

        protected void randomizeScreenImages()
        {
            InternalScreenSwitcher screenSwitcher;
            int screenIndex;

            //Seed the random number generator
            UnityEngine.Random.seed = System.Environment.TickCount;

            for (int index = 0; index < screenSwitchers.Count; index++)
            {
                //Get the screen switcher
                screenSwitcher = screenSwitchers[index];

                //If the screen is locked, then skip it.
                if (startingScreenImages.ContainsKey(index))
                {
                    if (startingScreenImages[index].locked)
                        continue;
                }

                //Generate a random screen image number
                screenIndex = UnityEngine.Random.Range(0, screenImages.Count);

                //Switch the texture
                screenSwitcher.ChangeTexture(screenImages[screenIndex].texture);
            }
        }

        protected void setupInitialImages()
        {
            if (startingScreenImages.Count == 0)
                return;

            for (int index = 0; index < screenSwitchers.Count; index++)
            {
                if (startingScreenImages.ContainsKey(index))
                    screenSwitchers[index].ChangeTexture(startingScreenImages[index].texture);
            }

        }

    }
}
