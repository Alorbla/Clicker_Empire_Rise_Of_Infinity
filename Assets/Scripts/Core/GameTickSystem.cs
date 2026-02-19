using System.Collections.Generic;
using UnityEngine;

public interface IGameTickListener
{
    void OnGameTick(float tickDelta);
}

public class GameTickSystem : MonoBehaviour
{
    public static GameTickSystem Instance { get; private set; }

    [SerializeField] private float tickStep = 0.2f;

    private readonly List<IGameTickListener> listeners = new List<IGameTickListener>();
    private readonly List<IGameTickListener> pendingAdd = new List<IGameTickListener>();
    private readonly List<IGameTickListener> pendingRemove = new List<IGameTickListener>();
    private float accumulator;
    private bool isTicking;

    public float TickStep => tickStep;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Register(IGameTickListener listener)
    {
        if (listener == null)
        {
            return;
        }

        if (isTicking)
        {
            if (!pendingAdd.Contains(listener))
            {
                pendingAdd.Add(listener);
            }
            return;
        }

        if (!listeners.Contains(listener))
        {
            listeners.Add(listener);
        }
    }

    public void Unregister(IGameTickListener listener)
    {
        if (listener == null)
        {
            return;
        }

        if (isTicking)
        {
            if (!pendingRemove.Contains(listener))
            {
                pendingRemove.Add(listener);
            }
            return;
        }

        listeners.Remove(listener);
    }

    private void Update()
    {
        if (tickStep <= 0f)
        {
            Tick(Time.deltaTime);
            return;
        }

        accumulator += Time.deltaTime;
        if (accumulator < tickStep)
        {
            return;
        }

        int safety = 0;
        while (accumulator >= tickStep)
        {
            accumulator -= tickStep;
            Tick(tickStep);

            if (++safety > 1000)
            {
                accumulator = 0f;
                break;
            }
        }
    }

    private void Tick(float step)
    {
        isTicking = true;
        for (int i = 0; i < listeners.Count; i++)
        {
            var listener = listeners[i];
            if (listener != null)
            {
                listener.OnGameTick(step);
            }
        }
        isTicking = false;

        if (pendingRemove.Count > 0)
        {
            for (int i = 0; i < pendingRemove.Count; i++)
            {
                listeners.Remove(pendingRemove[i]);
            }
            pendingRemove.Clear();
        }

        if (pendingAdd.Count > 0)
        {
            for (int i = 0; i < pendingAdd.Count; i++)
            {
                var listener = pendingAdd[i];
                if (listener != null && !listeners.Contains(listener))
                {
                    listeners.Add(listener);
                }
            }
            pendingAdd.Clear();
        }
    }
}
