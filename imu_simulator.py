#!/usr/bin/env python3
"""
IMU / tracker simulator for the laryngoscope.

Six sliders (X, Y, Z position + pitch, yaw, roll) are streamed over UDP to Unity,
exactly like a real sensor board (e.g. an ESP32) would stream its pose.

Packet format (one UDP packet per sample, plain text):
    x,y,z,pitch,yaw,roll
    - x, y, z in metres (offset from the laryngoscope's starting position)
    - pitch, yaw, roll in degrees

Run:
    python3 imu_simulator.py
Then set the Target IP in the window:
    127.0.0.1           -> Unity Editor / Meta XR Simulator on this Mac
    the Quest's Wi-Fi IP -> app running on the headset
"""

import random
import socket
import tkinter as tk

DEFAULT_HOST = "127.0.0.1"
DEFAULT_PORT = 5005
RATE_HZ = 60          # how often packets are sent (real IMUs are typically 50-200 Hz)
POS_RANGE_CM = 30     # slider range for position, +/- cm
ROT_RANGE_DEG = 180   # slider range for rotation, +/- degrees

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

root = tk.Tk()
root.title("Laryngoscope IMU Simulator")
root.resizable(False, False)

# ---------- target ----------
target = tk.Frame(root, padx=10, pady=8)
target.pack(fill="x")
tk.Label(target, text="Target IP").grid(row=0, column=0, sticky="w")
host_var = tk.StringVar(value=DEFAULT_HOST)
tk.Entry(target, textvariable=host_var, width=16).grid(row=0, column=1, padx=4)
tk.Label(target, text="Port").grid(row=0, column=2, sticky="w")
port_var = tk.StringVar(value=str(DEFAULT_PORT))
tk.Entry(target, textvariable=port_var, width=6).grid(row=0, column=3, padx=4)

# ---------- sliders ----------
sliders = {}

def make_slider(parent, name, rng, resolution, unit):
    row = tk.Frame(parent)
    row.pack(fill="x", pady=1)
    tk.Label(row, text=f"{name} ({unit})", width=11, anchor="w").pack(side="left")
    s = tk.Scale(row, from_=-rng, to=rng, resolution=resolution,
                 orient="horizontal", length=320)
    s.pack(side="left")
    sliders[name] = s

pos_frame = tk.LabelFrame(root, text="Position (offset from start)", padx=10, pady=4)
pos_frame.pack(fill="x", padx=10, pady=4)
for n in ("X", "Y", "Z"):
    make_slider(pos_frame, n, POS_RANGE_CM, 0.1, "cm")

rot_frame = tk.LabelFrame(root, text="Rotation", padx=10, pady=4)
rot_frame.pack(fill="x", padx=10, pady=4)
for n in ("Pitch", "Yaw", "Roll"):
    make_slider(rot_frame, n, ROT_RANGE_DEG, 1, "deg")

# ---------- options ----------
opts = tk.Frame(root, padx=10, pady=6)
opts.pack(fill="x")
sending_var = tk.BooleanVar(value=True)
tk.Checkbutton(opts, text="Sending", variable=sending_var).pack(side="left")
noise_var = tk.BooleanVar(value=False)
tk.Checkbutton(opts, text="Add sensor noise", variable=noise_var).pack(side="left", padx=8)

def reset():
    for s in sliders.values():
        s.set(0)

tk.Button(opts, text="Reset to zero", command=reset).pack(side="right")

status_var = tk.StringVar(value="")
tk.Label(root, textvariable=status_var, fg="gray30", padx=10, pady=6, anchor="w",
         font=("Menlo", 11)).pack(fill="x")

# ---------- send loop ----------
sent = 0

def tick():
    global sent
    x = sliders["X"].get() / 100.0   # cm -> m
    y = sliders["Y"].get() / 100.0
    z = sliders["Z"].get() / 100.0
    pitch = sliders["Pitch"].get()
    yaw = sliders["Yaw"].get()
    roll = sliders["Roll"].get()

    if noise_var.get():                       # roughly what a real sensor jitters by
        x += random.gauss(0, 0.002); y += random.gauss(0, 0.002); z += random.gauss(0, 0.002)
        pitch += random.gauss(0, 0.5); yaw += random.gauss(0, 0.5); roll += random.gauss(0, 0.5)

    packet = f"{x:.4f},{y:.4f},{z:.4f},{pitch:.2f},{yaw:.2f},{roll:.2f}"

    if sending_var.get():
        try:
            sock.sendto(packet.encode("ascii"), (host_var.get().strip(), int(port_var.get())))
            sent += 1
            status_var.set(f"sent #{sent}: {packet}")
        except Exception as e:
            status_var.set(f"error: {e}")
    else:
        status_var.set("paused")

    root.after(int(1000 / RATE_HZ), tick)

tick()
root.mainloop()
