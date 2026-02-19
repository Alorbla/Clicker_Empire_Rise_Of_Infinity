using System;
using System.Collections.Generic;
using UnityEngine;

public class DialogueEvents : MonoBehaviour
{
    private readonly Dictionary<string, Action> _events = new Dictionary<string, Action>();

    public void Raise(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return;
        }

        if (_events.TryGetValue(eventId, out var cb))
        {
            cb?.Invoke();
        }
    }

    public void Subscribe(string eventId, Action cb)
    {
        if (string.IsNullOrWhiteSpace(eventId) || cb == null)
        {
            return;
        }

        if (_events.TryGetValue(eventId, out var existing))
        {
            existing += cb;
            _events[eventId] = existing;
        }
        else
        {
            _events[eventId] = cb;
        }
    }

    public void Unsubscribe(string eventId, Action cb)
    {
        if (string.IsNullOrWhiteSpace(eventId) || cb == null)
        {
            return;
        }

        if (_events.TryGetValue(eventId, out var existing))
        {
            existing -= cb;
            if (existing == null)
            {
                _events.Remove(eventId);
            }
            else
            {
                _events[eventId] = existing;
            }
        }
    }
}
