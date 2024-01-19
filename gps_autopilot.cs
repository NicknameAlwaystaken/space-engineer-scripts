public Program()
{
    // Set the script to run the Main method every 100 game ticks (approximately every 1.6 seconds)
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

public void Main(string argument, UpdateType updateSource)
{
    IMyRemoteControl remoteControl = GetBlockWithName<IMyRemoteControl>("Remote Control");
    if (remoteControl == null)
    {
        Echo("Remote Control not found");
        return;
    }

    IMyLandingGear landingGear = GetBlockWithName<IMyLandingGear>("Landing Gear");
    if (landingGear == null)
    {
        Echo("Landing Gear not found");
        return;
    }

    // When the script is triggered by a player or terminal (not by the timer)
    if ((updateSource & UpdateType.Trigger) != 0 || (updateSource & UpdateType.Terminal) != 0)
    {
        if (!string.IsNullOrEmpty(argument))
        {
            if (argument.ToLower() == "raycast")
            {
                remoteControl.ClearWaypoints();
                PerformRaycast(remoteControl);
            }
            else
            {
                Vector3D liftOffPosition = CalculateLiftOffPosition(remoteControl);
                remoteControl.ClearWaypoints();
                remoteControl.AddWaypoint(liftOffPosition, "LiftOff");
                ProcessArgument(argument, remoteControl);
                remoteControl.ApplyAction("CollisionAvoidance_On");
                HandleLandingGear(landingGear);
                remoteControl.SetAutoPilotEnabled(true);
                Echo($"LiftOff Position: {liftOffPosition}");
            }
        }
    }

    // Check for last waypoint, but only if it's an update call (timer or automatic)
    if ((updateSource & UpdateType.Update100) != 0)
    {
        if (IsLastWaypointReached(remoteControl))
        {
            remoteControl.SetAutoPilotEnabled(false);
            remoteControl.ClearWaypoints();
            Echo("Last waypoint reached. Autopilot disabled. Waypoints cleared.");
        }
        else
        {
            Echo("Navigating to waypoint.");
        }
    }
}

// Method to handle landing gear
private void HandleLandingGear(IMyLandingGear landingGear)
{
    // Turn on the landing gear to ensure it's active
    landingGear.ApplyAction("OnOff_On");

    // If the landing gear is locked, unlock it
    if (landingGear.IsLocked)
    {
        landingGear.ApplyAction("Unlock");
    }
    else
    {
        // If the landing gear is not locked, and Auto-Lock is active, turn off Auto-Lock
        // Note: There isn't a direct way to check if Auto-Lock is on, so we assume it might be on
        landingGear.ApplyAction("Autolock_Off");
    }
}

// Perform a raycast and add the hit point as a waypoint
private void PerformRaycast(IMyRemoteControl remoteControl)
{
    IMyCameraBlock camera = GetBlockWithName<IMyCameraBlock>("Forward Camera");
    if (camera == null)
    {
        Echo("Camera not found");
        return;
    }

    MyDetectedEntityInfo hitInfo = camera.Raycast(100); // Raycast up to 10000 meters
    if (hitInfo.IsEmpty())
    {
        Echo("Raycast hit nothing");
        Echo(hitInfo.Type.ToString());
        return;
    }

    Vector3D hitPosition = hitInfo.HitPosition.Value;
    remoteControl.AddWaypoint(hitPosition, "Raycast Hit");
    Echo($"Raycast waypoint added: {hitPosition}");
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
    Vector3D directionVector = Vector3D.Normalize(gravityVector) * -1;
    return currentPosition + (directionVector * 50);
}

private void ProcessArgument(string argument, IMyRemoteControl remoteControl)
{
    string[] args = argument.Split(new char[] { ':' });
    if (args.Length >= 5 && args[0].ToLower() == "gps")
    {
        string gpsName = args[1];
        Vector3D newWaypointPosition;
        if (TryParseCoordinates(args[2], args[3], args[4], out newWaypointPosition))
        {
            remoteControl.AddWaypoint(newWaypointPosition, gpsName);
            Echo($"Added waypoint: {gpsName}");
        }
        else
        {
            Echo("Invalid GPS coordinates format.");
        }
    }
    else
    {
        Echo("Invalid argument format. Use 'gps:name:x:y:z'.");
    }
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
