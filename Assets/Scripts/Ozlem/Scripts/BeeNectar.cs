
using UnityEngine;

public class BeeNectar : MonoBehaviour
{
    private enum BeeState
    {
        Collecting,
        Waiting,
        Pollinating
    }

    [SerializeField] private PollenCapacitySO capacitySO;

    [Header("Collection")]
    [SerializeField] private float collectionStartDelay = 2f;
    [SerializeField] private float collectInterval = 0.5f;

    [Header("Pollination")]
    [SerializeField] private float pollinationDelay = 3f;
    [SerializeField] private float pollenSpreadInterval = 0.5f;

    [Header("Particle Effects")]
    [SerializeField] private ParticleSystem nectarCollectEffect;
    [SerializeField] private ParticleSystem pollenSpreadEffect;
    [SerializeField] private float collectParticleLifetime = 0.35f;

    [SerializeField] private int currentNectar;
    [SerializeField] private int capacity;

    private BeeState currentState = BeeState.Collecting;

    private float areaEnterTimer;
    private float collectTimer;
    private float stateTimer;

    public int CurrentNectar => currentNectar;
    public int Capacity => capacity;

    private void Awake()
    {
        PollenCapacity data = capacitySO.capacities.Find(
            x => x.type == PollenCapacityType.Bee
        );

        capacity = data != null ? data.capacity : 0;
        currentNectar = 0;
    }

    private void OnTriggerEnter(Collider other)
    {
        HiveArea hive = other.GetComponentInParent<HiveArea>();

        if (hive == null || currentNectar <= 0)
            return;

        int depositedAmount = hive.StorePollen(currentNectar);

        currentNectar -= depositedAmount;

        if (currentNectar <= 0)
        {
            currentState = BeeState.Collecting;
            areaEnterTimer = 0f;
            collectTimer = 0f;
            stateTimer = 0f;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        FlowerArea area = other.GetComponent<FlowerArea>();

        if (area == null)
            return;

        switch (currentState)
        {
            case BeeState.Collecting:
                CollectNectar(area);
                break;

            case BeeState.Waiting:
                WaitBeforePollination();
                break;

            case BeeState.Pollinating:
                SpreadPollen(area);
                break;
        }
    }

    private void CollectNectar(FlowerArea area)
    {
        if (currentNectar >= capacity)
        {
            currentState = BeeState.Waiting;
            stateTimer = 0f;
            return;
        }

        areaEnterTimer += Time.fixedDeltaTime;

        if (areaEnterTimer < collectionStartDelay)
            return;

        collectTimer += Time.fixedDeltaTime;

        if (collectTimer >= collectInterval)
        {
            if (area.TryCollectNectar(out Vector3 flowerPosition))
            {
                currentNectar++;
                PlayCollectionEffect(flowerPosition);
            }

            collectTimer = 0f;

            if (currentNectar >= capacity)
            {
                currentState = BeeState.Waiting;
                stateTimer = 0f;
            }
        }
    }

    private void WaitBeforePollination()
    {
        stateTimer += Time.fixedDeltaTime;

        if (stateTimer >= pollinationDelay)
        {
            currentState = BeeState.Pollinating;
            stateTimer = 0f;
        }
    }

    private void SpreadPollen(FlowerArea area)
    {
        stateTimer += Time.fixedDeltaTime;

        if (stateTimer < pollenSpreadInterval)
            return;

        if (currentNectar > 0)
        {
            currentNectar--;

            PlayPollenEffect();
            area.RegisterPollen(1);
        }

        stateTimer = 0f;

        if (currentNectar <= 0)
        {
            currentState = BeeState.Collecting;
            areaEnterTimer = 0f;
            collectTimer = 0f;
        }
    }

    private void PlayCollectionEffect(Vector3 flowerPosition)
    {
        if (nectarCollectEffect == null)
            return;

        Vector3 direction = transform.position - flowerPosition;
        float distance = direction.magnitude;

        if (distance <= 0.001f)
            return;

        ParticleSystem effect = Instantiate(
            nectarCollectEffect,
            flowerPosition,
            Quaternion.LookRotation(direction)
        );

        ParticleSystem.MainModule main = effect.main;

        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = distance / collectParticleLifetime;
        main.startLifetime = collectParticleLifetime;
        main.loop = false;

        effect.Play();

        Destroy(
            effect.gameObject,
            main.duration + collectParticleLifetime + 0.2f
        );
    }

    private void PlayPollenEffect()
    {
        if (pollenSpreadEffect == null)
            return;

        ParticleSystem effect = Instantiate(
            pollenSpreadEffect,
            transform.position,
            Quaternion.identity
        );

        ParticleSystem.MainModule main = effect.main;

        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop = false;

        effect.Play();

        Destroy(
            effect.gameObject,
            main.duration + main.startLifetime.constantMax + 0.2f
        );
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<FlowerArea>() == null)
            return;

        areaEnterTimer = 0f;
        collectTimer = 0f;
        stateTimer = 0f;
    }
}
