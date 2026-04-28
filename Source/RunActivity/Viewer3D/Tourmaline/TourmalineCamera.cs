using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Orts.Simulation.Physics;
using ORTS.Common;
using ORTS.Common.Input;

namespace Orts.Viewer3D.Tourmaline
{
    public class TourmalineCamera : LookAtCamera
    {
        public override string Name { get { return "Tourmaline"; } }
        // ====================== MODO DE ÓRBITA ======================        
        private float orbitSpeed = 0f;
        private bool isOrbitMode { get => Math.Abs(orbitSpeed) > 0.01f; }

        // ====================== VELOCIDADES POR TRANSICIÓN ======================
        private const float SpeedFast = 4.2f;
        private const float SpeedMedium = 1.6f;
        private const float SpeedSlow = 0.65f;

        // ====================== ESTADO INTERNO ======================
        private float currentDistance = 85f;
        private float currentAzimuth = 0f;
        private float currentElevation = 0.55f;

        private float targetDistance = 85f;
        private float targetAzimuth = 0f;
        private float targetElevation = 0.55f;

        private float currentMovementSpeed = SpeedMedium;

        private bool isMoving = false;
        private bool followTrainRotation = false;

        private Train followedTrain;

        public enum TourmalineView
        {
            Cenital,
            LateralIzquierda,
            LateralDerecha,
            TraseraElevada
        }

        public TourmalineCamera(Viewer viewer, Camera previousCamera)
            : base(viewer)
        {
            currentDistance = targetDistance = 85f;
            currentAzimuth = targetAzimuth = MathHelper.ToRadians(0f);
            currentElevation = targetElevation = MathHelper.ToRadians(35f);
        }

        // ====================== MÉTODOS DE VISTA ======================
        public void SetCenitalView()
        {
            if (followedTrain == null) return;
            orbitSpeed = 0f;

            float trainRotation = followedTrain.FrontTDBTraveller.RotY;

            targetDistance = 250f;
            targetAzimuth = trainRotation + MathHelper.Pi;        // cabeza arriba
            targetElevation = MathHelper.ToRadians(88.7f);

            currentMovementSpeed = SpeedFast;
            followTrainRotation = true;
            isMoving = true;
        }

        public void SetLateralView(bool izquierda)
        {
            orbitSpeed = 0f;
            targetDistance = 72f;
            targetAzimuth = izquierda ? MathHelper.ToRadians(0f) : MathHelper.ToRadians(180f);
            targetElevation = MathHelper.ToRadians(0.5f);          // Muy baja, como pediste
            currentMovementSpeed = SpeedMedium;
            followTrainRotation = false;
            isMoving = true;
        }

        public void SetTraseraElevadaView()
        {
            orbitSpeed = 0f;
            targetDistance = 72f;
            targetAzimuth = MathHelper.ToRadians(180f);
            targetElevation = MathHelper.ToRadians(24f);
            currentMovementSpeed = SpeedSlow;
            followTrainRotation = false;
            isMoving = true;
        }
        public void SetOrbitMode(float speed)
        {
            orbitSpeed = speed;
            Console.WriteLine($"[Tourmaline] Orbit mode {(isOrbitMode ? "activated" : "stopped")} - Speed: {speed:F1} deg/s");
        }

        // ====================== UPDATE ======================
        public override void Update(ElapsedTime elapsedTime)
        {
            if (followedTrain == null || followedTrain.Cars.Count == 0)
            {
                if (Viewer.SelectedTrain != null)
                    followedTrain = Viewer.SelectedTrain;
                return;
            }

            if (isOrbitMode)
            {
                currentAzimuth += MathHelper.ToRadians(orbitSpeed * elapsedTime.RealSeconds);

                //El ángulo debe quedar entre -pi y pi.
                while (currentAzimuth > MathHelper.Pi) currentAzimuth -= MathHelper.TwoPi;
                while (currentAzimuth < -MathHelper.Pi) currentAzimuth += MathHelper.TwoPi;
            }
            else
            {
                // Seguimiento continuo de rotación solo en vista cenital
                if (followTrainRotation && !isMoving)
                {
                    float trainRotation = followedTrain.FrontTDBTraveller.RotY;
                    targetAzimuth = trainRotation + MathHelper.Pi;
                    currentAzimuth = LerpAngle(currentAzimuth, targetAzimuth, elapsedTime.RealSeconds * 3.5f);
                }

                if (isMoving)
                {
                    float dt = elapsedTime.RealSeconds;

                    currentAzimuth = LerpAngle(currentAzimuth, targetAzimuth, dt * currentMovementSpeed);
                    currentElevation = MathHelper.Lerp(currentElevation, targetElevation, dt * currentMovementSpeed * 0.9f);
                    currentDistance = MathHelper.Lerp(currentDistance, targetDistance, dt * currentMovementSpeed * 1.2f);

                    if (Math.Abs(AngleDifference(currentAzimuth, targetAzimuth)) < 0.008f &&
                        Math.Abs(currentElevation - targetElevation) < 0.008f &&
                        Math.Abs(currentDistance - targetDistance) < 0.5f)
                    {
                        isMoving = false;
                        currentAzimuth = targetAzimuth;
                        currentElevation = targetElevation;
                        currentDistance = targetDistance;
                    }
                }
            }
            UpdateCameraPositionAndTarget();
            base.Update(elapsedTime);
            UpdateListener();
        }

        // ====================== NUEVO: CENTRO REAL DEL TREN ======================
        private void UpdateCameraPositionAndTarget()
        {
            if (followedTrain.Cars.Count == 0) return;

            // Calculamos el centro geométrico aproximado del tren
            Vector3 sum = Vector3.Zero;
            foreach (var car in followedTrain.Cars)
            {
                sum += car.WorldPosition.Location;
            }
            Vector3 trainCenter = sum / followedTrain.Cars.Count;

            // Elevamos ligeramente el punto de mira
            trainCenter.Y += 2.4f;

            float horiz = currentDistance * (float)Math.Cos(currentElevation);

            Vector3 offset = new Vector3(
                horiz * (float)Math.Sin(currentAzimuth),
                currentDistance * (float)Math.Sin(currentElevation),
                horiz * (float)Math.Cos(currentAzimuth)
            );

            cameraLocation = new WorldLocation(followedTrain.Cars[0].WorldPosition.WorldLocation);
            cameraLocation.Location = trainCenter + offset;
            cameraLocation.Normalize();

            targetLocation = new WorldLocation(followedTrain.Cars[0].WorldPosition.WorldLocation);
            targetLocation.Location = trainCenter;
            targetLocation.Normalize();
        }

        // ====================== AUXILIARES ======================
        private static float LerpAngle(float from, float to, float amount)
        {
            float diff = AngleDifference(to, from);
            return from + diff * MathHelper.Clamp(amount, 0f, 1f);
        }

        private static float AngleDifference(float a, float b)
        {
            float diff = a - b;
            while (diff > MathHelper.Pi) diff -= MathHelper.TwoPi;
            while (diff < -MathHelper.Pi) diff += MathHelper.TwoPi;
            return diff;
        }

        protected override void OnActivate(bool sameCamera)
        {
            if (followedTrain == null && Viewer.SelectedTrain != null)
                followedTrain = Viewer.SelectedTrain;

            isMoving = true;
            base.OnActivate(sameCamera);
        }

        public override void HandleUserInput(ElapsedTime elapsedTime)
        {
            //base.HandleUserInput(elapsedTime);

            if (UserInput.IsPressed(UserCommand.CameraPanUp))          // Flecha Arriba
            {
                SetCenitalView();
            }
            else if (UserInput.IsPressed(UserCommand.CameraPanDown))   // Flecha Abajo
            {
                SetTraseraElevadaView();
            }
            else if (UserInput.IsPressed(UserCommand.CameraPanLeft))   // Flecha Izquierda
            {
                SetLateralView(true);   // Izquierda
            }
            else if (UserInput.IsPressed(UserCommand.CameraPanRight))  // Flecha Derecha
            {
                SetLateralView(false);  // Derecha
            }
            else if (UserInput.IsPressed(UserCommand.CameraHeadOutBackward)) //Av Página
            {
                SetOrbitMode(4f);
            }
            else if (UserInput.IsPressed(UserCommand.CameraHeadOutForward)) //Av Página
            {
                SetOrbitMode(10f);
            }

        }
        
        }
}
