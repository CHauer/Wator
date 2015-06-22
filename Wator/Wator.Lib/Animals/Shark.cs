﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;

using Wator.Lib.World;

namespace Wator.Lib.Animals
{
    public class Shark : Animal
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Shark" /> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="field">The field.</param>
        public Shark(IWatorSettings settings, WatorField field)
            : base(settings, field)
        {
            this.Starve = 0; // "Hunger" 
        }

        /// <summary>
        /// Gets the breed time.
        /// </summary>
        /// <value>
        /// The breed time.
        /// </value>
        public override int BreedTime
        {
            get
            {
                return Settings.SharkBreedTime;
            }
        }

        /// <summary>
        /// Gets the color of the draw.
        /// </summary>
        /// <value>
        /// The color of the draw.
        /// </value>
        public override Color DrawColor
        {
            get
            {
                return this.Settings.SharkColor;
            }
        }

        /// <summary>
        /// Gets the starve.
        /// </summary>
        /// <value>
        /// The starve.
        /// </value>
        public int Starve { get; private set; }

        /// <summary>
        /// Steps of shark.
        /// </summary>
        public override void Step()
        {
            bool lockTaken = false;

            // increase lifetime
            this.Lifetime++;

            // find fish around
            var preyFieldDirection = GetRandomFishDirectionAround();
            var preyField = GetFieldFromDirection(preyFieldDirection);

            // shark eats a fish if found
            if (preyField != null)
            {
                try
                {
                    if (CheckLockRequired(preyFieldDirection, preyField))
                    {
                        Monitor.Enter(preyField, ref lockTaken);
                    }

                    // check again fish is on field - could be changed in meantime
                    if (preyField.Animal != null)
                    {
                        // clear own old animal space
                        this.Field.Animal = null;

                        // fish dies (clear animal.field and field.animal)
                        preyField.Animal.Die();

                        // fish is dead place shark on field
                        preyField.Animal = this;

                        // set fíeld as new place for shark
                        this.Field = preyField;
                    }
                }
                finally
                {
                    if (lockTaken)
                    {
                        Monitor.Exit(preyField);
                    }
                }
            }
            else
            {
                // if no fish found - increate starve 
                Starve++;

                if (Starve > Settings.SharkStarveTime)
                {
                    // shark dies (animal.field null and field.animal.null)
                    Die();

                    return;
                }

                //execute breed and move
                BreedMoveStep();
            }

            // set step down - animal moved
            this.IsMoved = true;
        }

        /// <summary>
        /// Creates the sibling depending on inherited type.
        /// </summary>
        /// <returns></returns>
        protected override Animal CreateSibling()
        {
            return new Shark(this.Settings, this.Field);
        }

        /// <summary>
        /// Find a random fish around.
        /// </summary>
        /// <returns></returns>
        protected Direction GetRandomFishDirectionAround()
        {
            this.FoundDirections.Clear();

            if (this.Field.NeighbourFieldDown.Animal is Fish)
            {
                this.FoundDirections.Add(Direction.Down);
            }

            if (this.Field.NeighbourFieldUp.Animal is Fish)
            {
                this.FoundDirections.Add(Direction.Up);
            }

            if (this.Field.NeighbourFieldLeft.Animal is Fish)
            {
                this.FoundDirections.Add(Direction.Left);
            }

            if (this.Field.NeighbourFieldRight.Animal is Fish)
            {
                this.FoundDirections.Add(Direction.Right);
            }

            if (this.FoundDirections.Count == 0)
            {
                // no field with fish found
                return Direction.None;
            }

            if (this.FoundDirections.Count == 1)
            {
                //only one field found
                return this.FoundDirections[0];
            }

            return this.FoundDirections[base.AnimalRandomizer.Next(0, this.FoundDirections.Count)];
        }

    }
}
