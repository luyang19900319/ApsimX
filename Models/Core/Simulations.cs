﻿using System.IO;
using System.Xml;
using Models.Core;
using System.Xml.Serialization;
using System;
using System.Reflection;
using System.Collections.Generic;
using Models.Factorial;

namespace Models.Core
{
    /// <summary>
    /// Encapsulates a collection of simulations. It is responsible for creating this collection,
    /// changing the structure of the components within the simulations, renaming components, adding
    /// new ones, deleting components. The user interface talks to an instance of this class.
    /// </summary>
    [Serializable]
    public class Simulations : Zone
    {
        private string _FileName;
        public Int32 ExplorerWidth { get; set; }

        /// <summary>
        /// The name of the file containing the simulations.
        /// </summary>
        [XmlIgnore]
        public string FileName
        {
            get
            {
                return _FileName;
            }
            set
            {
                _FileName = value;
            }
        }

        /// <summary>
        /// A list of all exceptions thrown during the creation and loading of the simulation.
        /// </summary>
        [XmlIgnore]
        public List<ApsimXException> LoadErrors { get; private set; }

        /// <summary>
        /// Create a simulations object by reading the specified filename
        /// </summary>
        public static Simulations Read(string FileName)
        {
            
            // Deserialise
            Simulations simulations = Utility.Xml.Deserialise(FileName) as Simulations;

            if (simulations != null)
            {
                // Set the filename
                simulations.FileName = FileName;
                simulations.SetFileNameInAllSimulations();

                // Parent all models.
                ParentAllModels(simulations);

                // Connect events and resolve links.
                simulations.AllModels.ForEach(ConnectEventPublishers);
                simulations.AllModels.ForEach(ResolveLinks);

                // Call OnLoaded in all models.
                simulations.AllModels.ForEach(CallOnLoaded);
            }
            else
                throw new Exception("Simulations.Read() failed. Invalid simulation file.\n");
            return simulations;
        }

        /// <summary>
        /// Write the specified simulation set to the specified filename
        /// </summary>
        public void Write(string FileName)
        {
            string tempFileName = Path.Combine(Path.GetTempPath(), Path.GetFileName(FileName));
            StreamWriter Out = new StreamWriter(tempFileName);
            Out.Write(Utility.Xml.Serialise(this, true));
            Out.Close();

            // If we get this far without an exception then copy the tempfilename over our filename,
            // creating a backup (.bak) in the process.
            string bakFileName = FileName + ".bak";
            File.Delete(bakFileName);
            if (File.Exists(FileName)) 
                File.Move(FileName, bakFileName);
            File.Move(tempFileName, FileName);
            this.FileName = FileName;
            SetFileNameInAllSimulations();
        }


        /// <summary>
        /// Constructor, private to stop developers using it. Use Simulations.Read instead.
        /// </summary>
        private Simulations() { }

        /// <summary>
        /// Find all simulations under the specified parent model.
        /// </summary>
        public static Simulation[] FindAllSimulationsToRun(Model parent)
        {
            List<Simulation> simulations = new List<Simulation>();

            if (parent is Experiment)
                simulations.AddRange((parent as Experiment).Create());
            else if (parent is Simulation)
                simulations.Add(parent as Simulation);
            else if (parent is ModelCollection)
            {
                ModelCollection modelCollection = parent as ModelCollection;

                // Look for simulations.
                foreach (Model model in modelCollection.AllModels)
                {
                    if (model is Experiment)
                        simulations.AddRange((model as Experiment).Create());
                    else if (model is Simulation && !(model.Parent is Experiment))
                        simulations.Add(model as Simulation);
                }
            }
            return simulations.ToArray();
        }

        /// <summary>
        /// Find all simulation names that are going to be run.
        /// </summary>
        /// <returns></returns>
        public string[] FindAllSimulationNames()
        {
            List<string> simulations = new List<string>();
            // Look for simulations.
            foreach (Model Model in AllModels)
            {
                if (Model is Simulation)
                {
                    // An experiment can have a base simulation - don't return that to caller.
                    if (!(Model.Parent is Experiment))
                        simulations.Add(Model.Name);
                }
            }

            // Look for experiments and get them to create their simulations.
            foreach (Model experiment in AllModels)
            {
                if (experiment is Experiment)
                    simulations.AddRange((experiment as Experiment).Names());
            }

            return simulations.ToArray();

        }

        /// <summary>
        /// Look through all models. For each simulation found set the filename.
        /// </summary>
        private void SetFileNameInAllSimulations()
        {
            foreach (Simulation simulation in AllModelsMatching(typeof(Simulation)))
                simulation.FileName = FileName;
        }



    }
}
