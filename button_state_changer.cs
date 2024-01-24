enum DroneState
{
    Idle,
    Leave,
    Return
}


DroneState currentState;

public Program()
{
    string savedState = Me.CustomData;
    if (!Enum.TryParse(savedState, out currentState))
    {
        currentState = DroneState.Idle; // Default state if parsing fails
    }

    Runtime.UpdateFrequency = UpdateFrequency.None; // Run only when triggered
}

public void Main(string argument)
{
    if (argument.Equals("idle", StringComparison.OrdinalIgnoreCase))
    {
        // Set the state to Idle if the argument is "idle"
        currentState = DroneState.Idle;
    }
    else
    {
        // Otherwise, cycle between Leave and Return states
        if (currentState == DroneState.Leave)
        {
            currentState = DroneState.Return;
        }
        else
        {
            currentState = DroneState.Leave;
        }
    }

    // Update the Custom Data with the new state
    Me.CustomData = currentState.ToString();

    // Output the current state
    Echo("Current State: " + currentState.ToString());
}

