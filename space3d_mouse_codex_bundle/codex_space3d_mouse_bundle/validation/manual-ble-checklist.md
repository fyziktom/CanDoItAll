# Manual Browser BLE Checklist

The user will perform this validation because browser BLE pairing must be done manually.

## Connection

1. Open the MouseLab page.
2. Click Connect BLE.
3. Pair with the Space3D mouse.
4. Confirm frame count increases.
5. Confirm source label is displayed.

## Orientation and calibration

1. Hold the device in neutral pose.
2. Capture Neutral.
3. Move yaw/pitch slowly.
4. Confirm pointer is stable near neutral and moves smoothly.
5. Press the recenter/calibrate button on the hardware and confirm generation/source status updates if exposed.

## Pan/zoom/rotate feel

1. Open the process workbench page.
2. Select Smooth default profile.
3. Hold button 1 and tilt/push for pan.
4. Hold button 2 and rotate wrist for orbit.
5. Hold button 3 and move forward/back for zoom.
6. Switch to Precision profile and confirm movements become finer.
7. Switch to Fast orbit profile and confirm rotation becomes faster without changing pan too much.

## Magnetometer comparison

Use serial or UI firmware settings if available:

1. Set orientation to `game`.
2. Reconnect/recenter and test yaw smoothness near the laptop.
3. Set orientation to `rotation`.
4. Reconnect/recenter and test yaw smoothness near the laptop.
5. Note whether magnetometer-assisted mode jumps or drifts less.

## Diagnostics

1. Confirm raw accel/gyro values change with movement.
2. Confirm filtered accel/gyro values are smoother than raw values.
3. Place the device still and verify stillness/bias status eventually becomes active.
4. Confirm small tremor below deadzone does not pan/zoom the scene.
