﻿using CommNet;
using CommNetConstellation.UI;
using Smooth.Algebraics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CommNetConstellation.CommNetLayer
{
    /// <summary>
    /// PartModule to be inserted into every part having ModuleCommand module (probe cores and manned cockpits)
    /// </summary>
    //This class is coupled with the MM patch (cnc_module_MM.cfg) that inserts CNConstellationModule into every command part
    public class CNConstellationModule : PartModule
    {
        [KSPEvent(guiActive = true, guiActiveEditor = true, guiActiveUnfocused = false, guiName = "CNC: Frequency List", active = true)]
        public void KSPEventVesselSetup()
        {
            new VesselSetupDialog("Vessel - <color=#00ff00>Frequency List</color>", this.vessel, null).launch();
        }
    }

    /// <summary>
    /// PartModule to be inserted into every part having ModuleDataTransmitter module (antennas, probe cores and manned cockpits)
    /// </summary>
    //This class is coupled with the MM patch (cnc_module_MM.cfg) that inserts CNConstellationAntennaModule into every part
    public class CNConstellationAntennaModule : PartModule
    {
        [KSPField(isPersistant = true)] public short Frequency = CNCSettings.Instance.PublicRadioFrequency;
        [KSPField(isPersistant = true)] protected string OptionalName = "";

        private ModuleDataTransmitter DTModule;
        public ModuleDataTransmitter DataTransmitter
        {
            get
            {
                if(DTModule == null && this.vessel.loaded && HighLogic.CurrentGame != null)
                    DTModule = this.part.FindModuleImplementing<ModuleDataTransmitter>();
                
                return DTModule;
            }
        }

        public String Name
        {
            get { return (this.OptionalName.Length == 0) ? this.part.partInfo.title : this.OptionalName; }
            set { this.OptionalName = value; }
        }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiActiveUnfocused = false, guiName = "CNC: Antenna setup", active = true)]
        public void KSPEventAntennaConfig()
        {
            new AntennaSetupDialog("Antenna - <color=#00ff00>Setup</color>", this.vessel, this.part).launch();
        }
    }

    /// <summary>
    /// Independent-implementation data structure for an antenna part
    /// </summary>
    public class CNCAntennaPartInfo
    {
        public short frequency;
        public string name;
        public double antennaPower;
        public double antennaCombinableExponent;
        public bool antennaCombinable;
        public AntennaType antennaType;
    }

    /// <summary>
    /// Data structure for a CommNetVessel
    /// </summary>
    public class CNCCommNetVessel : CommNetVessel
    {
        [Persistent(collectionIndex = "Frequency")] private Dictionary<short, double> FrequencyDict = new Dictionary<short, double>();

        //antenna data to use and display in vessel's management UI
        private List<CNConstellationAntennaModule> loadedAntennaList = new List<CNConstellationAntennaModule>();
        private List<ProtoPartModuleSnapshot> protoAntennaList = new List<ProtoPartModuleSnapshot>();

        /// <summary>
        /// Retrieve the CNC data from the vessel
        /// </summary>
        protected override void OnNetworkInitialized()
        {
            base.OnNetworkInitialized();

            try
            {
                validateAndUpgrade(this.Vessel);

                if (this.FrequencyDict.Count == 0) // empty list
                    buildDefaultFrequencyList();
            }
            catch (Exception e)
            {
                CNCLog.Error("Vessel '{0}' doesn't have any CommNet capability, likely a mislabelled junk or a kerbin on EVA", this.Vessel.GetName());
            }
        }

        protected void buildDefaultFrequencyList()
        {
            this.FrequencyDict.Clear();
            this.loadedAntennaList.Clear();
            this.protoAntennaList.Clear();

            int numParts = (!this.vessel.loaded) ? this.vessel.protoVessel.protoPartSnapshots.Count : this.vessel.Parts.Count;

            for (int i = 0; i < numParts; i++)
            {
                Part thisPart;
                ProtoPartSnapshot partSnapshot = null;

                if (!this.Vessel.loaded)
                {
                    thisPart = this.vessel.Parts[i];
                }
                else
                {
                    partSnapshot = this.vessel.protoVessel.protoPartSnapshots[i];
                    thisPart = partSnapshot.partInfo.partPrefab;
                }

                for(int pi = 0; pi < thisPart.Modules.Count; pi++)
                {
                    PartModule thisPartModule = thisPart.Modules[pi];
                    if (thisPartModule is ICommAntenna)
                    {
                        ICommAntenna thisAntenna = thisPartModule as ICommAntenna;
                        ProtoPartModuleSnapshot partModuleSnapshot = partSnapshot.FindModule(thisPartModule, pi);
                        double commPower = (!this.vessel.loaded) ? thisAntenna.CommPowerUnloaded(partModuleSnapshot) : thisAntenna.CommPower;

                        if (thisAntenna.CommCombinable)
                        {
                            double FreqCommPower += commPower * thisAntenna.CommCombinableExponent; // TODO: finish this
                            
                        }
                    }

                }
            }

            /*
            //cache antenna parts and build frequency list
            if (!this.Vessel.loaded)
            {
                List<ProtoPartSnapshot> parts = this.Vessel.protoVessel.protoPartSnapshots;
                for (int i = 0; i < parts.Count; i++)
                {
                    ProtoPartModuleSnapshot DTModule;
                    if ((DTModule=parts[i].FindModule("ModuleDataTransmitter")) != null) // check antennas, probe cores and manned cockpits
                    {
                        double commPower = double.Parse(DTModule.moduleValues.GetValue("antennaPower"));
                        string type = DTModule.moduleValues.GetValue("antennaType");
                        ProtoPartModuleSnapshot cncModule;
                        if ((cncModule = parts[i].FindModule("CNConstellationAntennaModule")) != null) //check if CNConstellationAntennaModule is there
                        {
                            protoAntennaList.Add(cncModule);
                            short freq = short.Parse(cncModule.moduleValues.GetValue("Frequency"));

                            if(!this.FrequencyDict.ContainsKey(freq))
                                FrequencyDict.Add(freq, );
                        }
                    }
                }
            }
            else
            {
                loadedAntennaList = this.Vessel.FindPartModulesImplementing<CNConstellationAntennaModule>();
                loadedAntennaList.ForEach(delegate (CNConstellationAntennaModule am) { frequencies.Add(am.Frequency); });
            }

            // remove duplicates and sort in asc order
            this.FrequencyDict = frequencies.Distinct().ToList();
            this.FrequencyDict.Sort();
            */

            CNCLog.Verbose("Frequency list of CommNet vessel '{0}' is built: {1}", this.Vessel.GetName(), UIUtils.Concatenate<short>(this.FrequencyDict, ", "));
        }

        /// <summary>
        /// Get the list of frequencies with optional flag to rebuild the list from the antennas
        /// </summary>
        public List<short> getFrequencies()
        {
            if(forceRebuild)
            {
                
            }

            return this.FrequencyDict; // by reference
        }

        /// <summary>
        /// Update the vessel's antenna(s) with the given frequency
        /// </summary>
        public bool updateAntennaFrequency(short oldFrequency, short newFrequency, bool rebuildFreqList = false)
        {
            try
            {
                if (!Constellation.isFrequencyValid(newFrequency))
                    throw new Exception(string.Format("The new frequency {0} is out of the range [0,{1}]!", newFrequency, short.MaxValue));

                if (this.Vessel.loaded)
                {
                    for (int i = 0; i < loadedAntennaList.Count; i++)
                    {
                        if (loadedAntennaList[i].Frequency == oldFrequency)
                            loadedAntennaList[i].Frequency = newFrequency;
                    }
                }
                else
                {
                    for (int i = 0; i < protoAntennaList.Count; i++)
                    {
                        if (short.Parse(protoAntennaList[i].moduleValues.GetValue("Frequency")) == oldFrequency)
                            protoAntennaList[i].moduleValues.SetValue("Frequency", newFrequency);
                    }
                }
            }
            catch(Exception e)
            {
                CNCLog.Error("Error encounted when updating CommNet vessel '{0}''s frequency {2} to {3}: {1}", this.Vessel.GetName() , e.Message, oldFrequency, newFrequency);
                return false;
            }

            getFrequencies(rebuildFreqList);

            CNCLog.Debug("Update CommNet vessel '{0}''s frequency {1} to {2}", this.Vessel.GetName(), oldFrequency, newFrequency);
            return true;
        }

        /// <summary>
        /// Check if given vessel has CNConstellationModule and its attributes required, and if not, "upgrade" the vessel data
        /// </summary>
        public void validateAndUpgrade(Vessel thisVessel)
        {
            if (thisVessel == null)
                return;

            if (!thisVessel.loaded) // it seems KSP will automatically add/upgrade the active vessel (unconfirmed)
            {
                CNCLog.Debug("Unloaded CommNet vessel '{0}' is validated and upgraded", thisVessel.GetName());

                List<ProtoPartSnapshot> parts = thisVessel.protoVessel.protoPartSnapshots;
                for (int i = 0; i < parts.Count; i++)
                {
                    if (parts[i].FindModule("ModuleCommand") != null) // check command parts only
                    {
                        ProtoPartModuleSnapshot cncModule;
                        if ((cncModule = parts[i].FindModule("CNConstellationModule")) == null) //check if CNConstellationModule is there
                        {
                            CNConstellationModule realcncModule = gameObject.AddComponent<CNConstellationModule>(); // don't use new keyword. PartModule is Monobehavior
                            parts[i].modules.Add(new ProtoPartModuleSnapshot(realcncModule));

                            CNCLog.Verbose("CNConstellationModule is added to CommNet Vessel '{0}'", thisVessel.GetName());
                        }
                        else //check if all attributes are or should not be there
                        {
                            if (cncModule.moduleValues.HasValue("radioFrequency")) //obsolete
                                cncModule.moduleValues.RemoveValue("radioFrequency");

                            if (cncModule.moduleValues.HasValue("communicationMembershipFlag")) //obsolete
                                cncModule.moduleValues.RemoveValue("communicationMembershipFlag");
                        }
                    }

                    if (parts[i].FindModule("ModuleDataTransmitter") != null) // check antennas, probe cores and manned cockpits
                    {
                        ProtoPartModuleSnapshot cncModule;
                        if ((cncModule = parts[i].FindModule("CNConstellationAntennaModule")) == null) //check if CNConstellationAntennaModule is there
                        {
                            CNConstellationAntennaModule realcncModule = gameObject.AddComponent<CNConstellationAntennaModule>(); // don't use new keyword. PartModule is Monobehavior
                            parts[i].modules.Add(new ProtoPartModuleSnapshot(realcncModule));

                            CNCLog.Verbose("CNConstellationAntennaModule is added to CommNet Vessel '{0}'", thisVessel.GetName());
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Independent-implemenation number of antennas detected
        /// </summary>
        public int getNumberAntennas()
        {
            if (this.Vessel.loaded)
                return loadedAntennaList.Count;
            else
                return protoAntennaList.Count;
        }

        /// <summary>
        /// Independent-implementation inforamtion on specific antenna
        /// </summary>
        public CNCAntennaPartInfo getAntennaInfo(int index)
        {
            CNCAntennaPartInfo newInfo = new CNCAntennaPartInfo();

            //loaded vessel
            if (this.Vessel.loaded)
            {
                CNConstellationAntennaModule AMod = loadedAntennaList[index];

                newInfo.frequency = AMod.Frequency;
                newInfo.name = AMod.Name;
                newInfo.antennaPower = AMod.DataTransmitter.antennaPower;
                newInfo.antennaCombinable = AMod.DataTransmitter.antennaCombinable;
                newInfo.antennaCombinableExponent = AMod.DataTransmitter.antennaCombinableExponent;
                newInfo.antennaType = AMod.DataTransmitter.antennaType;

                return newInfo; // eazy
            }

            //packed vessel
            ProtoPartModuleSnapshot packedAMod = protoAntennaList[index];
            ModuleDataTransmitter packedDTMod = (ModuleDataTransmitter) packedAMod.moduleRef; // really work?
            string optionalName = packedAMod.moduleValues.GetValue("OptionalName");

            newInfo.frequency = short.Parse(packedAMod.moduleValues.GetValue("Frequency"));
            newInfo.name = (optionalName.Length <= 0) ? packedAMod.moduleRef.part.partInfo.title : optionalName;
            newInfo.antennaPower = packedDTMod.antennaPower;
            newInfo.antennaCombinable = packedDTMod.antennaCombinable;
            newInfo.antennaCombinableExponent = packedDTMod.antennaCombinableExponent;
            newInfo.antennaType = packedDTMod.antennaType;

            return newInfo;
        }
    }
}
