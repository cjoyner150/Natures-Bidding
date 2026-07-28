using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class CoinMagnet : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform target;


    [Header("Movement")]
    [SerializeField] private float attractionDelay = 0.2f;
    [SerializeField] private float attractionStrength = 30f;
    [SerializeField] private float collectDistance = 0.25f;

    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles;

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();

        particles = new ParticleSystem.Particle[ps.main.maxParticles];
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        int count = ps.GetParticles(particles);

        float lifetime = ps.main.startLifetime.constant;

        for (int i = 0; i < count; i++)
        {
            Vector3 toTarget = target.position - particles[i].position;

            // Destroy the particle when it reaches the target.
            if (toTarget.sqrMagnitude <= collectDistance * collectDistance)
            {
                particles[i].remainingLifetime = 0f;

                // TODO:
                // Play a sound.
                // Increment your currency UI.
                // Spawn a sparkle effect.
                continue;
            }

            float age = lifetime - particles[i].remainingLifetime;

            // Wait before homing in.
            if (age < attractionDelay)
                continue;

            particles[i].velocity +=
                toTarget.normalized *
                attractionStrength *
                Time.deltaTime;
        }

        ps.SetParticles(particles, count);
    }

}