﻿using System;
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
    public class WBIInflatablePartModule : ExtendedPartModule
    {
        [KSPField(isPersistant = true)]
        public bool flightAnimationOnly;

        [KSPField(isPersistant = true)]
        public string animationName;

        [KSPField(isPersistant = true)]
        public string startEventGUIName;

        [KSPField(isPersistant = true)]
        public string endEventGUIName;

        [KSPField]
        public bool overridePartAttachRestriction = false;

        [KSPField()]
        public bool isInflatable = false;

        //Helper objects
        public bool animationStarted = false;
        public bool isDeployed = false;
        public int inflatedCrewCapacity = 0;

        #region User Events & API
        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "ToggleInflation", externalToEVAOnly = false, unfocusedRange = 3.0f, guiActiveUnfocused = true)]
        public virtual void ToggleInflation()
        {
            //If the module is inflatable, deployed, and has kerbals inside, then don't allow the module to be deflated.
            if (isInflatable && isDeployed && this.part.protoModuleCrew.Count() > 0)
            {
                ScreenMessages.PostScreenMessage(this.part.name + " has crew aboard. Vacate the module before deflating it.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            //make sure we don't have parts radially attached
            if (isInflatable && isDeployed && !overridePartAttachRestriction)
            {
                if (this.part.children.Count > 0)
                {
                    foreach (Part childPart in this.part.children)
                        if (childPart.attachMode == AttachModes.SRF_ATTACH)
                        {
                            ScreenMessages.PostScreenMessage(this.part.name + " has parts attached to it. Please remove them before deflating.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                            return;
                        }
                }
            }

            //Play animation for current state
            animationStarted = true;
            PlayAnimation(isDeployed);
            
            //Toggle state
            isDeployed = !isDeployed;
            if (isDeployed)
            {
                this.part.CrewCapacity = inflatedCrewCapacity;
                Events["ToggleInflation"].guiName = endEventGUIName;
            }
            else
            {
                this.part.CrewCapacity = 0;
                Events["ToggleInflation"].guiName = startEventGUIName;

                //Turn off the lights if deflating the module.
                WBILight light = this.part.FindModuleImplementing<WBILight>();
                if (light != null)
                {
                    if (light.isDeployed)
                        light.TurnOffLights();
                }
            }

            Log("Animation toggled new gui name: " + Events["ToggleInflation"].guiName);
        }
        #endregion

        #region Overrides
        public override void OnLoad(ConfigNode node)
        {
            string value;
            base.OnLoad(node);

            value = node.GetValue("isDeployed");
            if (string.IsNullOrEmpty(value) == false)
                isDeployed = bool.Parse(value);

            value = node.GetValue("isInflatable");
            if (string.IsNullOrEmpty(value) == false)
                isInflatable = bool.Parse(value);

            value = node.GetValue("inflatedCrewCapacity");
            if (string.IsNullOrEmpty(value) == false)
                inflatedCrewCapacity = int.Parse(value);

            try
            {
                SetupAnimations();
            }

            catch (Exception ex)
            {
                Log("Error encountered while attempting to setup animations: " + ex.ToString());
            }
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            node.AddValue("isDeployed", isDeployed.ToString());
            node.AddValue("isInflatable", isDeployed.ToString());
            node.AddValue("inflatedCrewCapacity", inflatedCrewCapacity.ToString());
        }

        protected override void getProtoNodeValues(ConfigNode protoNode)
        {
            base.getProtoNodeValues(protoNode);
            string value;

            //isInflatable
            value = protoNode.GetValue("isInflatable");
            if (string.IsNullOrEmpty(value) == false)
                isInflatable = bool.Parse(value);

            animationName = protoNode.GetValue("animationName");

            endEventGUIName = protoNode.GetValue("endEventGUIName");

            startEventGUIName = protoNode.GetValue("startEventGUIName");

            value = protoNode.GetValue("inflatedCrewCapacity");
            if (string.IsNullOrEmpty(value) == false)
            {
                inflatedCrewCapacity = int.Parse(value);
                if (isInflatable && isDeployed && HighLogic.LoadedSceneIsFlight)
                    this.part.CrewCapacity = inflatedCrewCapacity;
            }
        }

/*
        public override void OnFixedUpdate()
        {
            base.OnFixedUpdate();
            this.Events["ToggleInflation"].guiActive = false;
            this.Events["ToggleInflation"].guiActiveEditor = false;
            this.Events["ToggleInflation"].guiActiveUnfocused = false;
        }
*/
        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            SetupAnimations();

            if (string.IsNullOrEmpty(animationName))
            {
                this.Events["ToggleInflation"].guiActive = false;
                this.Events["ToggleInflation"].guiActiveEditor = false;
                this.Events["ToggleInflation"].guiActiveUnfocused = false;
            }

            else if (flightAnimationOnly)
                Events["ToggleInflation"].guiActiveEditor = false;
        }
        #endregion

        #region Helpers
        public virtual void SetupAnimations()
        {
            Log("SetupAnimations called.");

            //Show the toggle animation button
            //and set up the animation
            if (isInflatable)
            {
                Log("Part is inflatable, looking for animations.");
                Animation[] animations = this.part.FindModelAnimators(animationName);
                if (animations == null)
                    return;

                Animation anim = animations[0];
                if (anim == null)
                    return;

                //Set layer
                anim[animationName].layer = 1;

                //Set toggle button
                Events["ToggleInflation"].guiActive = true;
                Events["ToggleInflation"].guiActiveEditor = true;
                Events["ToggleInflation"].guiActiveUnfocused = true;

                if (isDeployed)
                {
                    Events["ToggleInflation"].guiName = endEventGUIName;

                    //Make sure the inflatable module is fully deployed.
                    anim[animationName].normalizedTime = 1.0f;
                    anim[animationName].speed = 10000f;
                    if (HighLogic.LoadedSceneIsFlight)
                        this.part.CrewCapacity = inflatedCrewCapacity;
                }
                else
                {
                    Events["ToggleInflation"].guiName = startEventGUIName;

                    //Make sure the inflatable module is fully retracted.
                    anim[animationName].normalizedTime = 0f;
                    anim[animationName].speed = -10000f;
                    if (HighLogic.LoadedSceneIsFlight)
                        this.part.CrewCapacity = 0;
                }
                anim.Play(animationName);
            }

            //Hide toggle button
            else
            {
                Log("Part is not inflatable.");
                Events["ToggleInflation"].guiActive = false;
                Events["ToggleInflation"].guiActiveEditor = false;
                Events["ToggleInflation"].guiActiveUnfocused = false;
            }
        }

        public virtual void PlayAnimation(bool playInReverse = false)
        {
            float animationSpeed = playInReverse == false ? 1.0f : -1.0f;
            Animation anim = this.part.FindModelAnimators(animationName)[0];

            if (playInReverse)
            {
                anim[animationName].time = anim[animationName].length;
                anim[animationName].speed = animationSpeed;
                anim.Play(animationName);
            }

            else
            {
                anim[animationName].speed = animationSpeed;
                anim.Play(animationName);
            }
        }
        #endregion
    }
}
