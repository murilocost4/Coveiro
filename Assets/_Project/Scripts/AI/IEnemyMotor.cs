using UnityEngine;

namespace OCoveiro.AI
{
    /// <summary>
    /// Como um inimigo se move. A IA (EnemyBrain) só pede "vá até ali", "pare", "vire";
    /// cada motor resolve do seu jeito (padrão Strategy):
    /// - NavMeshMotor: anda pelo NavMesh e contorna paredes (Errantes).
    /// - GhostMotor: flutua em linha reta e atravessa paredes (Espectro).
    /// Só se move no host; nos outros PCs a posição chega pelo NetworkTransform.
    /// </summary>
    public interface IEnemyMotor
    {
        void Initialize(bool isServer);
        void MoveTo(Vector3 destination, float speed);
        void Stop();
        void FaceTowards(Vector3 point, float degreesPerSecond);
        void AddImpulse(Vector3 velocity);
        void Warp(Vector3 position, Quaternion rotation);
        bool HasArrived(float tolerance);
    }
}
