﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

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
    public class ResourceHelper
    {
        public static float GetResourceMass(Part part)
        {
            PartResourceDefinitionList definitions = PartResourceLibrary.Instance.resourceDefinitions;
            PartResourceList resources = part.Resources;
            PartResourceDefinition resourceDef;
            float totalResourceMass = 0f;

            foreach (PartResource resource in resources)
            {
                //Find definition
                resourceDef = definitions[resource.name];

                if (resourceDef != null)
                    totalResourceMass += (float)(resourceDef.density * resource.amount);
            }

            return totalResourceMass;
        }

        public static float GetResourceCost(Part part)
        {
            PartResourceDefinitionList definitions = PartResourceLibrary.Instance.resourceDefinitions;
            PartResourceList resources = part.Resources;
            PartResourceDefinition resourceDef;
            float totalCost = 0f;

            foreach (PartResource resource in resources)
            {
                //Find definition
                resourceDef = definitions[resource.resourceName];
                if (resourceDef != null)
                    totalCost += (float)(resourceDef.unitCost * resource.maxAmount);

            }

            return totalCost;
        }

        public static void SetResourceValues(string resourceName, Part part, float amount, float maxAmout)
        {
            PartResourceDefinitionList definitions = PartResourceLibrary.Instance.resourceDefinitions;
            List<PartResource> resources;
            int resourceID;

            //First, does the resource definition exist?
            if (definitions.Contains(resourceName))
            {
                resources = new List<PartResource>();
                resourceID = definitions[resourceName].id;

                //The definition exists, now see if the part has the resource
                part.GetConnectedResources(resourceID, ResourceFlowMode.NULL, resources);

                //Now go through and set the values
                foreach (PartResource resource in resources)
                {
                    resource.amount = amount;
                    resource.maxAmount = maxAmout;
                }
            }
        }

        public static void RemoveResource(string resourceName, Part part)
        {
            PartResourceDefinitionList definitions = PartResourceLibrary.Instance.resourceDefinitions;
            List<PartResource> resources;
            int resourceID;

            //First, does the resource definition exist?
            if (definitions.Contains(resourceName))
            {
                resources = new List<PartResource>();
                resourceID = definitions[resourceName].id;

                //The definition exists, now see if the part has the resource
                part.GetConnectedResources(resourceID, ResourceFlowMode.NULL, resources);

                //Now go through and remove the resources
                foreach (PartResource doomed in resources)
                    part.Resources.list.Remove(doomed);
            }
        }

        public static void AddResource(string resourceName, float amount, float maxAmount, Part part)
        {
            PartResourceDefinitionList definitions = PartResourceLibrary.Instance.resourceDefinitions;
            PartResource newResource = new PartResource();

            //First, does the resource definition exist?
            if (definitions.Contains(resourceName))
            {
                newResource.part = part;
                newResource.SetInfo(definitions[resourceName]);
                newResource.amount = amount;
                newResource.maxAmount = maxAmount;

                part.Resources.list.Add(newResource);
            }
        }

        public static void DepleteResource(string resourceName, Part part)
        {
            PartResourceDefinitionList definitions = PartResourceLibrary.Instance.resourceDefinitions;
            List<PartResource> resources;
            int resourceID;

            //First, does the resource definition exist?
            if (definitions.Contains(resourceName))
            {
                resources = new List<PartResource>();
                resourceID = definitions[resourceName].id;

                //The definition exists, now see if the part has the resource
                part.GetConnectedResources(resourceID, ResourceFlowMode.NULL, resources);

                //Now go through and deplete the resources
                foreach (PartResource resource in resources)
                {
                    resource.amount = 0f;
                }
            }
        }

        public static double ConsumeResource(List<PartResource> resources, double amountRequested)
        {
            double amountAcquired = 0;
            double amountRemaining = amountRequested;

            foreach (PartResource resource in resources)
            {
                //Do we have more than enough?
                if (resource.amount >= amountRemaining)
                {
                    //We got what we wanted, yay. :)
                    amountAcquired += amountRemaining;

                    //reduce the part resource's current amount
                    resource.amount -= amountRemaining;

                    //Done
                    break;
                }

                //PartResource's amount < amountRemaining
                //Drain the resource dry
                else
                {
                    amountAcquired += resource.amount;

                    resource.amount = 0;
                }
            }

            return amountAcquired;
        }

        public static PartResourceDefinition DefinitionForResource(string resourceName)
        {
            PartResourceDefinitionList definitions = PartResourceLibrary.Instance.resourceDefinitions;

            if (definitions.Contains(resourceName))
                return definitions[resourceName];

            return null;
        }

        public static bool VesselHasResource(string resourceName, Vessel vessel)
        {
            PartResourceDefinitionList definitions = PartResourceLibrary.Instance.resourceDefinitions;
            List<PartResource> resources;
            List<Part> parts;
            int resourceID;

            //First, does the resource definition exist?
            if (definitions.Contains(resourceName))
            {
                resources = new List<PartResource>();
                resourceID = definitions[resourceName].id;

                //The definition exists, now see if the vessel has the resource
                parts = vessel.parts;
                foreach (Part part in parts)
                {
                    part.GetConnectedResources(resourceID, ResourceFlowMode.NULL, resources);

                    //If somebody has the resource, then we're good.
                    if (resources.Count > 0)
                        return true;
                }
            }

            return false;
        }

        public static List<PartResource> GetConnectedResources(string resourceName, Part part)
        {
            PartResourceDefinitionList definitions = PartResourceLibrary.Instance.resourceDefinitions;
            List<PartResource> resources = new List<PartResource>();
            int resourceID;

            if (definitions.Contains(resourceName))
            {
                resourceID = definitions[resourceName].id;
                part.GetConnectedResources(resourceID, ResourceFlowMode.NULL, resources);
            }

            return resources;
        }

        public static List<PartResource> GetConnectedResources(int resourceID, Part part)
        {
            List<PartResource> resources = new List<PartResource>();

            part.GetConnectedResources(resourceID, ResourceFlowMode.NULL, resources);

            return resources;
        }

        public static float CapacityRemaining(List<PartResource> resources)
        {
            float capacityRemaining = 0;

            foreach (PartResource resource in resources)
                capacityRemaining += (float)(resource.maxAmount - resource.amount);

            return capacityRemaining;
        }

        public static float DistributeResource(List<PartResource> resources, float amount)
        {
            float remainingAmount = amount;
            float amountPerContainer = amount / resources.Count;

            foreach (PartResource resource in resources)
            {
                //Does the resource container have enough room?
                if ((resource.maxAmount - resource.amount) >= amountPerContainer)
                {
                    resource.amount += amountPerContainer;
                    remainingAmount -= amountPerContainer;
                }
            }

            return remainingAmount;
        }

        public static float DistributeResource(string resourceName, float amount, Part part)
        {
            List<PartResource> resources = GetConnectedResources(resourceName, part);

            if (resources == null)
                return amount;

            return DistributeResource(resources, amount);
        }
    }
}
