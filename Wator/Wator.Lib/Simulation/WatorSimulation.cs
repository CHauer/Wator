﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Wator.Lib.Images;
using Wator.Lib.World;

namespace Wator.Lib.Simulation
{
    public class WatorSimulation : IDisposable
    {
        /// <summary>
        /// The simulation thread
        /// </summary>
        private Thread simulationThread;

        /// <summary>
        /// The phase randomizer
        /// </summary>
        private Random phaseRandomizer;

        /// <summary>
        /// The black/white phase as array
        /// </summary>
        private Phase[] simulationPhases;

        /// <summary>
        /// The step watch
        /// </summary>
        private Stopwatch stepWatch;

        /// <summary>
        /// The current fish popluation
        /// </summary>
        private static int currentFishPopluation;

        /// <summary>
        /// The current shark popluation
        /// </summary>
        private static int currentSharkPopluation;

        /// <summary>
        /// Initializes a new instance of the <see cref="WatorSimulation"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public WatorSimulation(WatorSettings settings)
        {
            // stop obj creation if settings wrong
            CheckSettings(settings);

            // create wator world 
            InitializeWatorWorld(settings);

            // save settings in this instance
            this.Settings = settings;

            InitializeSimulationThread();

            // initialize image creator
            InitializeImageCreator();

            // intialize concurrency of simulation (phases)
            InitializeConcurrency();

            // intialize stop watches
            InitializeTimeTracking();
        }

        #region Properties

        /// <summary>
        /// Gets a value indicating whether this instance is running.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is running; otherwise, <c>false</c>.
        /// </value>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Gets the image creator.
        /// </summary>
        /// <value>
        /// The image creator.
        /// </value>
        public ImageCreator<WatorWorld> ImageCreator { get; private set; }

        /// <summary>
        /// Gets the is end reached.
        /// </summary>
        /// <value>
        /// The is end reached.
        /// </value>
        public bool IsEndReached { get; private set; }

        /// <summary>
        /// Gets the settings.
        /// </summary>
        /// <value>
        /// The settings.
        /// </value>
        public IWatorSettings Settings { get; private set; }

        /// <summary>
        /// Gets the wator world.
        /// </summary>
        /// <value>
        /// The wator world.
        /// </value>
        public WatorWorld WatorWorld { get; private set; }

        /// <summary>
        /// Gets the current simulation round.
        /// </summary>
        /// <value>
        /// The round.
        /// </value>
        public int Round { get; private set; }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when the end of wator simulation is reached.
        /// No sharks/fish left.
        /// </summary>
        public event EventHandler EndReached;

        /// <summary>
        /// Occurs when a simulation step is done.
        /// </summary>
        public event EventHandler<SimulationState> StepDone;

        #endregion

        /// <summary>
        /// Starts the simulation.
        /// </summary>
        public void StartSimulation()
        {
            this.IsRunning = true;

            this.ImageCreator.StartCreator();
            this.simulationThread.Start();
        }

        /// <summary>
        /// Stops the simulation.
        /// </summary>
        public void StopSimulation()
        {
            this.IsRunning = false;

            this.ImageCreator.StopCreator();

            // cancel simulation - fire threadabortexcep
            this.simulationThread.Abort();
            // wait for thread exit
            this.simulationThread.Join();
        }

        /// <summary>
        /// Runs the simulation.
        /// </summary>
        private void RunSimulation()
        {
            while (this.IsRunning)
            {
                try
                {
                    stepWatch.Start();

                    // perform step
                    SimulationStep();

                    stepWatch.Stop();

                    // create image of step
                    ImageCreator.AddJob(new ImageJob<WatorWorld>(this.WatorWorld, this.Round));

                    //Increase round
                    Round++;

                    //simulation step done
                    OnStepDone(
                        new SimulationState()
                            {
                                FishPopulation = currentFishPopluation,
                                SharkPopulation = currentSharkPopluation,
                                Round = Round,
                                StepTime = stepWatch.Elapsed
                            });

                    stepWatch.Reset();
                }
                catch (ThreadAbortException ex)
                {
                    Debug.WriteLine("Simluation Thread aborted.");
                    return;
                }
            }
        }

        /// <summary>
        /// Run one Simulation step.
        /// </summary>
        private void SimulationStep()
        {
            int firstPhase = phaseRandomizer.Next(0, 1);
            int secondPhase = firstPhase == 0 ? 1 : 0; // opposite

            // Start phases
            simulationPhases[firstPhase].Start();
            simulationPhases[firstPhase].WaitForEnd();

            simulationPhases[secondPhase].Start();
            simulationPhases[secondPhase].WaitForEnd();

            // reset moved stats
            WatorWorld.FinishSteps();
        }

        /// <summary>
        /// Changes the fish population.
        /// </summary>
        /// <param name="increase">if set to <c>true</c> increase otherwise decrease.</param>
        public static void ChangeFishPopulation(bool increase = true)
        {
            if (increase)
            {
                Interlocked.Increment(ref currentFishPopluation);
            }
            Interlocked.Decrement(ref currentFishPopluation);
        }

        /// <summary>
        /// Changes the shark population.
        /// </summary>
        /// <param name="increase">if set to <c>true</c> increase otherwise decrease.</param>
        public static void ChangeSharkPopulation(bool increase = true)
        {
            if (increase)
            {
                Interlocked.Increment(ref currentSharkPopluation);
            }
            Interlocked.Decrement(ref currentSharkPopluation);
        }

        #region Initialize

        /// <summary>
        /// Initializes the concurrency.
        /// Creates phases, splits up world in "rows" -> phase execution workers
        /// sets up tasks for execution
        /// waits for simulation to start
        /// </summary>
        private void InitializeConcurrency()
        {
            simulationPhases = new Phase[]
            {
                new Phase(WatorWorld, true), // black phase
                new Phase(WatorWorld,false) // white phase
            };

            phaseRandomizer = new Random(DateTime.Now.Millisecond);
        }

        /// <summary>
        /// Checks the settings.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Please enter a valid Wator Fields Height!
        /// or
        /// Please enter a valid Wator Fields Width!</exception>
        private void CheckSettings(IWatorSettings settings)
        {
            if (settings.WorldHeight <= 0)
            {
                throw new ArgumentOutOfRangeException("settings", "Please enter a valid Wator Fields Height!");
            }

            if (settings.WorldWidth <= 0)
            {
                throw new ArgumentOutOfRangeException("settings", "Please enter a valid Wator Fields Width!");
            }

            //// condition for splitting world in "rows/phases" for paralellization
            //if (settings.WorldHeight % 2 != 0)
            //{
            //    throw new ArgumentOutOfRangeException("settings", "Please enter a valid even number as Wator Fields Height!");
            //}

            //if ((settings.WorldHeight / (Environment.ProcessorCount * 3)) > 2)
            //{
            //    throw new ArgumentOutOfRangeException("settings", "Please enter a valid even number as Wator Fields Height!");
            //}
        }

        /// <summary>
        /// Initializes the wator world.
        /// </summary>
        /// <param name="settings">The settings.</param>
        private void InitializeWatorWorld(IWatorSettings settings)
        {
            WatorSimulation.currentSharkPopluation = settings.InitialSharkPopulation;
            WatorSimulation.currentFishPopluation = settings.InitialFishPopulation;
            this.IsEndReached = false;
            this.Round = 0;
            this.WatorWorld = new WatorWorld(settings);
        }

        /// <summary>
        /// Initializes the simulation.
        /// </summary>
        private void InitializeSimulationThread()
        {
            this.simulationThread = new Thread(RunSimulation);
            this.IsRunning = false;
        }

        /// <summary>
        /// Initializes the image creator.
        /// </summary>
        private void InitializeImageCreator()
        {
            this.ImageCreator = new ImageCreator<WatorWorld>(this.Settings);
        }

        /// <summary>
        /// Initializes the time tracking.
        /// </summary>
        private void InitializeTimeTracking()
        {
            this.stepWatch = new Stopwatch();
        }

        #endregion

        /// <summary>
        /// Called when end reached.
        /// </summary>
        protected virtual void OnEndReached()
        {
            var handler = this.EndReached;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben aus, 
        /// die mit dem Freigeben, Zurückgeben oder 
        /// Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        public void Dispose()
        {
            if (this.simulationThread.IsAlive)
            {
                this.IsRunning = false;
                this.simulationThread.Abort();
            }
            this.simulationThread = null;
        }

        /// <summary>
        /// Called when a simulation step is done.
        /// </summary>
        /// <param name="e">The e.</param>
        protected virtual void OnStepDone(SimulationState e)
        {
            var handler = this.StepDone;

            if (handler != null)
            {
                handler(this, e);
            }
        }
    }
}
