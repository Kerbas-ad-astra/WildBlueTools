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
    public delegate void TransmitComplete();

    public struct TransmitItem
    {
        public float science;
        public float reputation;
        public float funds;
        public string title;
    }

    public class TransmitHelper
    {
        protected const string kNoAvailableTransmitter = "No Comms Devices on this vessel. Cannot Transmit Data.";
        protected const string kSoldData = "<color=lime>Added <b>{0:f2}</b> Funds to your budget.</color>";
        protected const string kPublishedData = "<color=yellow>Your Reputation has improved by <b>{0:f2}</b></color>";
        protected const string kSciencedData = "<color=lightblue>Added <b>{0:f2}</b> Science to your budget.</color>";

        public TransmitComplete transmitCompleteDelegate = null;
        public Part part = null;

        protected List<TransmitItem> transmitList = new List<TransmitItem>();

        public bool TransmitToKSC(ScienceData data)
        {
            List<ModuleDataTransmitter> transmitters = this.part.vessel.FindPartModulesImplementing<ModuleDataTransmitter>();
            List<ScienceData> dataQueue = new List<ScienceData>();
            ModuleDataTransmitter bestTransmitter = null;

            //Package up the data and put it in the queue.
            TransmitItem item = new TransmitItem();
            item.science = data.dataAmount;
            item.reputation = 0f;
            item.funds = 0f;
            item.title = data.title;
            transmitList.Add(item);

            //Find an available transmitter. if found, transmit the data.
            dataQueue.Add(data);
            foreach (ModuleDataTransmitter transmitter in transmitters)
            {
                if (transmitter.IsBusy() == false)
                {
                    if (bestTransmitter == null)
                        bestTransmitter = transmitter;
                    else if (transmitter.packetSize > bestTransmitter.packetSize)
                        bestTransmitter = transmitter;
                }
            }

            if (bestTransmitter != null)
            {
                bestTransmitter.TransmitData(dataQueue, OnTransmitComplete);
                return true;
            }
            else
            {
                //Inform user that there is no available transmitter.
                ScreenMessages.PostScreenMessage(kNoAvailableTransmitter, 5.0f, ScreenMessageStyle.UPPER_CENTER);
                return false;
            }
        }

        public bool TransmitToKSC(float science, float reputation, float funds, float dataAmount = -1f)
        {
            float transmitSize = dataAmount;
            List<ModuleDataTransmitter> transmitters = this.part.vessel.FindPartModulesImplementing<ModuleDataTransmitter>();
            List<ScienceData> dataQueue = new List<ScienceData>();
            ModuleDataTransmitter bestTransmitter = null;

            if (transmitSize == -1f)
            {
                if (science > 0f)
                    transmitSize = science / 0.3f;
                else if (reputation > 0f)
                    transmitSize = reputation / 0.3f;
                else
                    transmitSize = funds / 0.3f;
            }

            else
            {
                transmitSize = dataAmount;
            }

            ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment("crewReport");
            ScienceSubject subject = ResearchAndDevelopment.GetExperimentSubject(experiment, ExperimentSituations.SrfLanded, FlightGlobals.GetHomeBody(), "");
            ScienceData data = new ScienceData(transmitSize, 0f, 0, subject.id, "");

            //Package up the data and put it in the queue.
            TransmitItem item = new TransmitItem();
            item.science = science;
            item.reputation = reputation;
            item.funds = funds;
            transmitList.Add(item);

            //Find an available transmitter. if found, transmit the data.
            dataQueue.Add(data);
            foreach (ModuleDataTransmitter transmitter in transmitters)
            {
                if (transmitter.IsBusy() == false)
                {
                    if (bestTransmitter == null)
                        bestTransmitter = transmitter;
                    else if (transmitter.packetSize > bestTransmitter.packetSize)
                        bestTransmitter = transmitter;
                }
            }

            if (bestTransmitter != null)
            {
                bestTransmitter.TransmitData(dataQueue, OnTransmitComplete);
                return true;
            }
            else
            {
                //Inform user that there is no available transmitter.
                ScreenMessages.PostScreenMessage(kNoAvailableTransmitter, 5.0f, ScreenMessageStyle.UPPER_CENTER);
                return false;
            }
        }

        public void OnTransmitComplete()
        {
            string transmitMessage = "";

            //Get the top item off the list
            TransmitItem item = transmitList[0];
            transmitList.RemoveAt(0);

            if (item.science > 0f)
            {
                transmitMessage = string.Format(kSciencedData, item.science);
                if ((HighLogic.CurrentGame.Mode == Game.Modes.CAREER || HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX)
                    && ResearchAndDevelopment.Instance != null)
                    ResearchAndDevelopment.Instance.AddScience(item.science, TransactionReasons.ScienceTransmission);
                ScreenMessages.PostScreenMessage(transmitMessage, 5.0f, ScreenMessageStyle.UPPER_LEFT);
            }

            if (item.reputation > 0f)
            {
                transmitMessage = string.Format(kPublishedData, item.reputation);
                if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER && Reputation.Instance != null)
                    Reputation.Instance.AddReputation(item.reputation, TransactionReasons.ScienceTransmission);
                ScreenMessages.PostScreenMessage(transmitMessage, 5.0f, ScreenMessageStyle.UPPER_LEFT);
            }

            if (item.funds > 0f)
            {
                transmitMessage = string.Format(kSoldData, item.funds);
                Funding.Instance.AddFunds(item.funds, TransactionReasons.ScienceTransmission);
                ScreenMessages.PostScreenMessage(transmitMessage, 5.0f, ScreenMessageStyle.UPPER_LEFT);
            }

            if (transmitCompleteDelegate != null)
                transmitCompleteDelegate();
        }

    }
}
