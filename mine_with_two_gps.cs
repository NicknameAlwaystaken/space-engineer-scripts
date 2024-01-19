enum DroneState
{
    Idle,
    Leave,
    Return
}

DroneState currentState = DroneState.Idle;
DroneState lastTaskDone = DroneState.Idle;

DroneState switcherState = DroneState.Idle;

public Program()
{
    // Set the script to run the Main method every 100 game ticks (approximately every 1.6 seconds)
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

public void Main(string argument, UpdateType updateSource)
{
    StringBuilder echoStringBuilder = new StringBuilder();

    IMyRemoteControl remoteControl = GetBlockWithName<IMyRemoteControl>("Remote Control");
    if (remoteControl == null)
    {
        echoStringBuilder.AppendLine("Remote Control not found");
        Echo(echoStringBuilder.ToString());
        return;
    }
    else {
        PrintWaypoints(remoteControl, echoStringBuilder);
    }

    // Retrieve the current state from the Switcher
    switcherState = GetStateFromSwitcher();
    
    // Update the state only if it's different from the current and the last task completed
    if (switcherState != currentState && switcherState != lastTaskDone)
    {
        currentState = switcherState;
        echoStringBuilder.AppendLine("New State: " + currentState.ToString());
    }
    else
    {
        echoStringBuilder.AppendLine("No new state");
    }

    // When the script is triggered by a player or terminal (not by the timer)
    if ((updateSource & UpdateType.Trigger) != 0 || (updateSource & UpdateType.Terminal) != 0)
    {
        if (!string.IsNullOrEmpty(argument) && argument.ToLower().StartsWith("gps"))
        {
            remoteControl.ClearWaypoints();
            ProcessArgument(argument, remoteControl);
            remoteControl.ApplyAction("CollisionAvoidance_On");
            remoteControl.SetAutoPilotEnabled(true);
            echoStringBuilder.AppendLine("Waypoints set for route.");
        }
        else
        {
            echoStringBuilder.AppendLine("Argument invalid, give 2 gps values");
        }
    }

    // Check for last waypoint, but only if it's an update call (timer or automatic)
    if ((updateSource & UpdateType.Update100) != 0)
    {
        echoStringBuilder.AppendLine("Current state: " + currentState.ToString());
        // Process the route based on the current state
        switch (currentState)
        {
            case DroneState.Leave:
                echoStringBuilder.AppendLine("Leave state active");
                if (lastTaskDone != DroneState.Leave)
                {
                    HandleLeaveState(remoteControl);
                    lastTaskDone = DroneState.Leave;
                }
                break;

            case DroneState.Return:
                echoStringBuilder.AppendLine("Return state active");
                if (lastTaskDone != DroneState.Return)
                {
                    HandleReturnState(remoteControl);
                    lastTaskDone = DroneState.Return;
                }
                break;
        }
    }
    // Check if the last waypoint has been reached
    if (IsLastWaypointReached(remoteControl))
    {
        remoteControl.SetAutoPilotEnabled(false);
        remoteControl.ClearWaypoints();
        echoStringBuilder.AppendLine("Last waypoint reached. Autopilot disabled.");
    }

    Echo(echoStringBuilder.ToString());
}

private void PrintWaypoints(IMyRemoteControl remoteControl, StringBuilder echoStringBuilder)
{
    List<MyWaypointInfo> waypoints = new List<MyWaypointInfo>();
    remoteControl.GetWaypointInfo(waypoints);

    if (waypoints.Count == 0)
    {
        echoStringBuilder.AppendLine("No waypoints set.");
        return;
    }

    echoStringBuilder.AppendLine("Waypoints in order:");
    foreach (var waypoint in waypoints)
    {
        echoStringBuilder.AppendLine($"{waypoint.Name}");
    }
}



private DroneState GetStateFromSwitcher()
{
    var switcher = GridTerminalSystem.GetBlockWithName("Switcher") as IMyProgrammableBlock;
    if (switcher == null)
    {
        Echo("Switcher not found");
        return DroneState.Idle; // Default to Idle if not found
    }

    DroneState switcherState;
    if (Enum.TryParse(switcher.CustomData, out switcherState))
    {
        return switcherState;
    }
    else
    {
        return DroneState.Idle; // Default to Idle if parsing fails
    }
}

private void HandleLeaveState(IMyRemoteControl remoteControl)
{
    Vector3D home, homeLiftOff, destination, destinationLiftoff;
    if (RetrieveGpsFromCustomData(out home, out homeLiftOff, out destinationLiftoff, out destination))
    {
        remoteControl.ClearWaypoints();
        // Add waypoints in order for leave journey
        remoteControl.AddWaypoint(home, "Home");
        remoteControl.AddWaypoint(homeLiftOff, "Home LiftOff");
        remoteControl.AddWaypoint(destinationLiftoff, "Destination Liftoff");
        remoteControl.AddWaypoint(destination, "Destination");
        remoteControl.SetAutoPilotEnabled(true);
    }
    else
    {
        Echo("Failed to retrieve GPS data for leave journey.");
    }

    lastTaskDone = DroneState.Leave;
}


private void HandleReturnState(IMyRemoteControl remoteControl)
{
    Vector3D home, homeLiftOff, destination, destinationLiftoff;
    if (RetrieveGpsFromCustomData(out home, out homeLiftOff, out destinationLiftoff, out destination))
    {
        remoteControl.ClearWaypoints();
        // Add waypoints in reverse order for return journey
        remoteControl.AddWaypoint(destination, "Destination");
        remoteControl.AddWaypoint(destinationLiftoff, "Destination Liftoff");
        remoteControl.AddWaypoint(homeLiftOff, "Home LiftOff");
        remoteControl.AddWaypoint(home, "Home");
        remoteControl.SetAutoPilotEnabled(true);
    }
    else
    {
        Echo("Failed to retrieve GPS data for return journey.");
    }

    lastTaskDone = DroneState.Return;
}


private bool RetrieveGpsFromCustomData(out Vector3D home, out Vector3D homeLiftOff, out Vector3D destinationLiftoff, out Vector3D destination)
{
    string customData = Me.CustomData;
    string[] entries = customData.Split(new[] { "GPS:" }, StringSplitOptions.RemoveEmptyEntries);

    home = homeLiftOff = destinationLiftoff = destination = new Vector3D();

    if (entries.Length < 4) // Expecting at least 4 entries
    {
        Echo("Not enough GPS data in Custom Data.");
        return false;
    }

    // The order of retrieval should match the order of appending
    return TryParseGpsEntry(entries[0], out home) &&
           TryParseGpsEntry(entries[1], out homeLiftOff) &&
           TryParseGpsEntry(entries[2], out destinationLiftoff) &&
           TryParseGpsEntry(entries[3], out destination);
}


private bool TryParseGpsEntry(string entry, out Vector3D coordinates)
{
    // Extract the part inside parentheses
    int startIndex = entry.IndexOf('(');
    int endIndex = entry.IndexOf(')');
    if (startIndex == -1 || endIndex == -1 || endIndex <= startIndex)
    {
        Echo("Invalid GPS entry format.");
        coordinates = new Vector3D();
        return false;
    }

    string coordsPart = entry.Substring(startIndex + 1, endIndex - startIndex - 1);
    string[] parts = coordsPart.Split(':');

    return TryParseCoordinates(parts[0], parts[1], parts[2], out coordinates);
}




// Method to handle landing gear
private void HandleLandingGear(IMyLandingGear landingGear)
{
    if (landingGear != null) {
        // Turn on the landing gear to ensure it's active
        landingGear.ApplyAction("OnOff_On");

        // Unlock landing gear
        landingGear.ApplyAction("Unlock");
    }
    else {
        Echo("Landing Gear not found");
    }
}

// Function to check if the last waypoint is reached
private bool IsLastWaypointReached(IMyRemoteControl remoteControl)
{
    var waypoints = new List<MyWaypointInfo>();
    remoteControl.GetWaypointInfo(waypoints); // This will fill the list with the current waypoints

    // If no waypoints are present, we assume the last has been reached
    if (waypoints.Count == 0)
        return true;

    // Alternatively, check if the drone is close enough to the last waypoint
    MyWaypointInfo lastWaypoint = waypoints[waypoints.Count - 1];
    double distanceToLastWaypoint = Vector3D.Distance(remoteControl.GetPosition(), lastWaypoint.Coords);
    
    // If the distance to the last waypoint is less than a certain threshold, consider it reached
    if (distanceToLastWaypoint < 5.0) // Threshold distance can be changed as needed
    {
        return true;
    }

    return false;
}


private T GetBlockWithName<T>(string name) where T : class
{
    T block = GridTerminalSystem.GetBlockWithName(name) as T;
    if (block == null)
    {
        Echo($"{typeof(T).Name} named '{name}' not found.");
    }
    return block;
}

private Vector3D CalculateLiftOffPosition(IMyRemoteControl remoteControl)
{
    Vector3D currentPosition = remoteControl.GetPosition();
    Vector3D gravityVector = remoteControl.GetNaturalGravity();

    // Check if the gravity vector is not zero
    if (gravityVector.Length() > 0)
    {
        Vector3D directionVector = Vector3D.Normalize(gravityVector) * -1;
        return currentPosition + (directionVector * 50); // 50 meters above the current position
    }
    else
    {
        // If there's no natural gravity, return the current position
        // This means lift-off position will be the same as the home position
        return currentPosition;
    }
}


private void ProcessArgument(string argument, IMyRemoteControl remoteControl)
{
    string[] args = argument.Split(new char[] { ':' });

    if (args.Length < 7) // Minimum length to include at least one set of coordinates
    {
        Echo("Invalid argument format. Not enough data for GPS locations.");
        return;
    }

    int i = 0;
    Vector3D? destination = null;
    Vector3D? destinationLiftoff = null;
    int gpsCount = 0;

    while (i < args.Length && gpsCount < 2) // Process only the first two sets of GPS coordinates
    {
        if (args[i].ToLower() == "gps")
        {
            int offset = i + 2; // Skip "gps" and the name
            Vector3D waypoint;
            if (offset + 3 < args.Length && TryParseCoordinates(args[offset], args[offset + 1], args[offset + 2], out waypoint))
            {
                if (gpsCount == 0)
                    destinationLiftoff = waypoint; // First GPS argument is destination liftoff
                else
                    destination = waypoint; // Second GPS argument is destination

                gpsCount++;
                i = offset + 3; // Move past the coordinates

                // Skip the color code if present
                i += SkipColorCode(args, i);
            }
            else
            {
                Echo("Invalid GPS coordinates format.");
                return;
            }
        }
        else
        {
            i++; // Skip to the next part
        }
    }

    if (destination == null || destinationLiftoff == null)
    {
        Echo("Not enough GPS coordinates provided.");
    }
    else
    {
        // Successfully processed two sets of GPS coordinates
        Vector3D currentPos = remoteControl.GetPosition(); // Current drone position (Home)
        Vector3D liftOffPosition = CalculateLiftOffPosition(remoteControl); // Home LiftOff

        remoteControl.ClearWaypoints();

        // Append GPS data to Custom Data
        AppendGpsToCustomData(currentPos, liftOffPosition, destinationLiftoff.Value, destination.Value);
    }
}


private int SkipColorCode(string[] args, int index)
{
    // Check if the next element looks like a color code and skip it if so
    return (index + 1 < args.Length && args[index + 1].StartsWith("#")) ? 1 : 0;
}

private void AppendGpsToCustomData(Vector3D home, Vector3D homeLiftOff, Vector3D destinationLiftoff, Vector3D destination)
{
    string customData = "";
    customData += $"GPS:Home({home.X}:{home.Y}:{home.Z}), ";
    customData += $"GPS:Home LiftOff({homeLiftOff.X}:{homeLiftOff.Y}:{homeLiftOff.Z}), ";
    customData += $"GPS:Destination Liftoff({destinationLiftoff.X}:{destinationLiftoff.Y}:{destinationLiftoff.Z}), ";
    customData += $"GPS:Destination({destination.X}:{destination.Y}:{destination.Z}), ";
    Me.CustomData = customData;
}




private bool TryParseCoordinates(string x, string y, string z, out Vector3D result)
{
    double xCoord, yCoord, zCoord;
    result = new Vector3D();
    bool xValid = double.TryParse(x, out xCoord);
    bool yValid = double.TryParse(y, out yCoord);
    bool zValid = double.TryParse(z, out zCoord);

    if (xValid && yValid && zValid)
    {
        result = new Vector3D(xCoord, yCoord, zCoord);
        return true;
    }
    else
    {
        Echo("Invalid GPS coordinates.");
        return false;
    }
}
