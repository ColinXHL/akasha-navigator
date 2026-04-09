using AkashaNavigator.Views.Windows;

namespace AkashaNavigator.Services;

public sealed class ControlBarDecision
{
    public ControlBarDisplayState NextState { get; init; }
    public bool StartHideDelayTimer { get; init; }
    public bool StopHideDelayTimer { get; init; }
}

public class ControlBarDisplayController
{
    public ControlBarDisplayState State { get; private set; } = ControlBarDisplayState.Hidden;
    public DateTime LastStateChangeUtc { get; private set; } = DateTime.MinValue;

    public void SetState(ControlBarDisplayState state, DateTime nowUtc)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        LastStateChangeUtc = nowUtc;
    }

    public ControlBarDecision EvaluateMouse(bool isMouseOverWindow,
                                            bool isMouseInTopTriggerZone,
                                            bool isContextMenuOpen,
                                            bool isUrlTextBoxFocused,
                                            DateTime nowUtc)
    {
        if ((nowUtc - LastStateChangeUtc).TotalMilliseconds < AppConstants.ControlBarStateStabilityMs)
        {
            return new ControlBarDecision { NextState = State };
        }

        return State switch
        {
            ControlBarDisplayState.Hidden => new ControlBarDecision
            {
                NextState = isMouseInTopTriggerZone ? ControlBarDisplayState.TriggerLine : State
            },
            ControlBarDisplayState.TriggerLine => EvaluateTriggerLineState(isMouseOverWindow, isMouseInTopTriggerZone),
            ControlBarDisplayState.Expanded => EvaluateExpandedState(isMouseOverWindow,
                                                                     isContextMenuOpen,
                                                                     isUrlTextBoxFocused),
            _ => new ControlBarDecision { NextState = State }
        };
    }

    public ControlBarDecision EvaluateHideDelay(bool isMouseOverWindow,
                                                bool isMouseInTopTriggerZone,
                                                bool isContextMenuOpen,
                                                bool isUrlTextBoxFocused,
                                                DateTime nowUtc)
    {
        _ = isMouseInTopTriggerZone;
        _ = nowUtc;

        if (isMouseOverWindow || isContextMenuOpen || isUrlTextBoxFocused)
        {
            return new ControlBarDecision
            {
                NextState = State,
                StopHideDelayTimer = true
            };
        }

        return new ControlBarDecision { NextState = ControlBarDisplayState.Hidden };
    }

    private ControlBarDecision EvaluateTriggerLineState(bool isMouseOverWindow, bool isMouseInTopTriggerZone)
    {
        if (isMouseOverWindow)
        {
            return new ControlBarDecision
            {
                NextState = ControlBarDisplayState.Expanded,
                StopHideDelayTimer = true
            };
        }

        if (!isMouseInTopTriggerZone)
        {
            return new ControlBarDecision
            {
                NextState = State,
                StartHideDelayTimer = true
            };
        }

        return new ControlBarDecision
        {
            NextState = State,
            StopHideDelayTimer = true
        };
    }

    private ControlBarDecision EvaluateExpandedState(bool isMouseOverWindow,
                                                     bool isContextMenuOpen,
                                                     bool isUrlTextBoxFocused)
    {
        if (isMouseOverWindow || isContextMenuOpen || isUrlTextBoxFocused)
        {
            return new ControlBarDecision
            {
                NextState = State,
                StopHideDelayTimer = true
            };
        }

        return new ControlBarDecision
        {
            NextState = State,
            StartHideDelayTimer = true
        };
    }
}
