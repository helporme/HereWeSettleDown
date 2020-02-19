﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using UnityEngine;
using Helper;

namespace Generator
{
    public class MasterGenerator : MonoBehaviour
    {
        public int seed;

        public static bool GeneratorsRegistered { get; private set; }
        public static bool GenerationEnd { get; private set; }

        private SubGenerator[] registratedGenerators;
        private Dictionary<SubGenerator, GeneratorRegData> generatorsRegData = new Dictionary<SubGenerator, GeneratorRegData>();
        private Dictionary<int, List<SubGenerator>> generatorsPriority = new Dictionary<int, List<SubGenerator>>();
        private Dictionary<SubGenerator, Thread> generatorThreads = new Dictionary<SubGenerator, Thread>();

        private static Dictionary<SubGenerator, bool> generatorStates = new Dictionary<SubGenerator, bool>();
        private static Dictionary<string, bool> generatorStringStates = new Dictionary<string, bool>();


        protected float generationStartedAt;
        protected float generationEndedAt;

        private void Update()
        {
            if (!GenerationEnd)
            {
                CheckForGenerationEnd();
            }
        }

        public void RegistrateGenerators()
        {
            ClearRegistratedGenerators();
            System.Random mainPrng = new System.Random(seed);

            registratedGenerators = ObjectFinder.FindRootObjects<SubGenerator>();
            foreach (SubGenerator gen in registratedGenerators)
            {
                if (!generatorsRegData.ContainsKey(gen))
                {
                    // Add generator to registered dictionary
                    GeneratorRegData data = GetGeneratorData(gen);
                    generatorsRegData[gen] = data;

                    // Add generator to priority dictionary
                    if (!generatorsPriority.ContainsKey(data.priority))
                        generatorsPriority[data.priority] = new List<SubGenerator>();
                    generatorsPriority[data.priority].Add(gen);

                    // Add generator to state dictionary
                    generatorStates[gen] = false;
                    generatorStringStates[gen.GetType().FullName] = false;

                    // Add SubGenerator prng
                    gen.ownPrng = new System.Random(mainPrng.Next(int.MinValue, int.MaxValue));
                    gen.OnRegistrate();
                }
            }
            GeneratorsRegistered = true;
        }

        public GeneratorRegData GetGeneratorData(SubGenerator generator)
        {
            GeneratorRegData regData = new GeneratorRegData();

            object[] attributes = generator.GetType().GetCustomAttributes(typeof(CustomGenerator), false);
            if (attributes.Length > 0)
            {
                CustomGenerator genAttr = (CustomGenerator)attributes[0];
                regData = genAttr.data;
            }

            return regData;
        }

        public void ClearRegistratedGenerators()
        {
            generatorsRegData.Clear();
            generatorsPriority.Clear();
            generatorStates.Clear();
            GeneratorsRegistered = false;
        }

        public void StartGenerate()
        {
            generationStartedAt = Time.time;

            int[] keys = generatorsPriority.Keys.ToArray();
            Array.Sort(keys);

            foreach (int priority in keys)
            {
                foreach (SubGenerator gen in generatorsPriority[priority])
                {
                    StartCoroutine(ActivateGenerator(gen, generatorsRegData[gen]));
                }
            }
        }

        private IEnumerator ActivateGenerator(SubGenerator generator, GeneratorRegData regData)
        {
            yield return new WaitUntil(() => CheckDataValues(regData));
            if (regData.useThread)
            {
                // Create own thread to SubGenerator
                Thread thread = new Thread(generator.OnGenerate)
                {
                    IsBackground = true,
                    Name = generator.name
                };

                Debug.Log(generator.name + " in own thread stated.");
                thread.Start();
                generatorThreads[generator] = thread;
            }
            else
            {
                // Run in main Thread
                Debug.Log(generator.name + " in main thread stated.");
                generator.OnGenerate();
            }
        }

        private bool CheckDataValues(GeneratorRegData data)
        {
            if (data.requireValues == null && data.requiredCompletedGenerators == null)
                return false;

            if (data.requireValues != null)
            {
                if (data.requireValues.Contains("Nothing"))
                    return true;

                foreach (string requireValue in data.requireValues)
                {
                    if (!SubGenerator.values.ContainsKey(requireValue))
                        return false;
                }
            }
           
            if (data.requiredCompletedGenerators != null)
            {
                foreach (Type requireGenerator in data.requiredCompletedGenerators)
                {
                    if (!generatorStringStates[requireGenerator.FullName])
                        return false;
                }
            }

            return true;
        }

        public static void SetGeneratorState(SubGenerator generator, bool state)
        {
            if (GeneratorsRegistered)
            {
                if (generatorStates.ContainsKey(generator))
                {
                    generatorStates[generator] = state;
                    generatorStringStates[generator.GetType().FullName] = state;
                    if (state)
                        Debug.Log(generator.GetType().Name + " generation completed."); 
                }
            }
            else
                Debug.LogWarning("Attempt to change the state of the generator before registering it.");
        }

        private void CheckForGenerationEnd()
        {
            if (GeneratorsRegistered && !generatorStates.Values.Contains(false))
            {
                generationEndedAt = Time.time;
                Debug.Log("Time has passed since the start of generation " + (generationEndedAt - generationStartedAt));

                GenerationEnd = true;
                OnGenerationEnd();
            }
        }

        public virtual void OnGenerationEnd() { }
    }
}
