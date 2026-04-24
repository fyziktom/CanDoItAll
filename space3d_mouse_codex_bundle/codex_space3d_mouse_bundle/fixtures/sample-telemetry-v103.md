# Sample Telemetry Fixture Notes

Use the existing `MouseLab.razor` demo-frame builder or create a dedicated test-frame builder in the test project.

Minimum fixture properties:

- Protocol major: 1.
- Protocol minor: 3.
- Message type: `0x31`.
- Valid SysEx envelope: `F0 ... F7`.
- Valid manufacturer byte: `0x7D`.
- Magic: `IDRM`.
- CRC computed with the same XOR-based CRC7 helper used by firmware/host.

Recommended test cases:

1. Neutral quaternion, zero gyro, zero accel, no buttons.
2. Non-neutral quaternion, positive gyro values, positive/negative accel values.
3. Pressed button bitmask with ADC click event.
4. Corrupted CRC.
5. Truncated frame.
6. Unknown message type.

Do not hardcode only one happy-path frame. Protocol parsing should be tested against edge cases.
