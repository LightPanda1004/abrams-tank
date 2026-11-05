using UnityEngine;
using System.Collections.Generic;

namespace TankValley
{
    // Uses physical keys directly so the sample works without an Input System package.
    public sealed class ValleyFlyCamera : MonoBehaviour
    {
        public float moveSpeed = 55f;
        public float lookSpeed = 0.16f;
        private Vector2 previousMouse;
        private bool looking;
        private readonly HashSet<KeyCode> held = new HashSet<KeyCode>();

        private void Update()
        {
            Vector3 v = Vector3.zero;
            if (held.Contains(KeyCode.W)) v += transform.forward;
            if (held.Contains(KeyCode.S)) v -= transform.forward;
            if (held.Contains(KeyCode.D)) v += transform.right;
            if (held.Contains(KeyCode.A)) v -= transform.right;
            if (held.Contains(KeyCode.E)) v += Vector3.up;
            if (held.Contains(KeyCode.Q)) v -= Vector3.up;
            float speed = held.Contains(KeyCode.LeftShift) ? moveSpeed * 3f : moveSpeed;
            transform.position += Vector3.ClampMagnitude(v, 1f) * speed * Time.deltaTime;
        }

        private void OnApplicationFocus(bool focus) { if (!focus) { held.Clear(); looking = false; } }

        private void OnGUI()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown) held.Add(e.keyCode);
            if (e.type == EventType.KeyUp) held.Remove(e.keyCode);
            if (e.type == EventType.MouseDown && e.button == 1)
            {
                looking = true;
                previousMouse = e.mousePosition;
            }
            if (e.type == EventType.MouseUp && e.button == 1) looking = false;
            if (looking && e.type == EventType.MouseDrag && e.button == 1)
            {
                Vector2 delta = e.mousePosition - previousMouse;
                previousMouse = e.mousePosition;
                transform.Rotate(Vector3.up, delta.x * lookSpeed, Space.World);
                transform.Rotate(Vector3.right, delta.y * lookSpeed, Space.Self);
            }
            GUI.Box(new Rect(16, 16, 390, 68), "TANK VALLEY  /  ENVIRONMENT PREVIEW\nWASD move | Q/E height | Shift boost | Right-drag look\nEnvironment scene - add your own tank controller.");
        }
    }
}
