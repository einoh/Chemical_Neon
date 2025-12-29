# Fix: Arduino HMAC.h Not Found Error

## Problem
```
fatal error: HMAC.h: No such file or directory
#include <HMAC.h>
```

This means the **Crypto library is not installed** in your Arduino IDE.

## Solution: Install Crypto Library

### Method 1: Using Arduino IDE Library Manager (Recommended) ?

1. **Open Arduino IDE**
2. Go to: **Sketch** ? **Include Library** ? **Manage Libraries**
   - Or use keyboard shortcut: `Ctrl + Shift + I`
3. A "Library Manager" window will open
4. In the search box at the top, type: `Crypto`
5. Look for the entry: **"Crypto"** by **Tisham Dhar**
   - Version should be 0.4.4 or later
6. Click **Install**
7. Wait for installation to complete (usually 1-2 minutes)
8. Click **Close**

? **Library is now installed!**

### Verify Installation

1. Go to: **Sketch** ? **Include Library**
2. Scroll down through the list
3. You should see **"Crypto"** listed under "Contributed libraries"

? **You're ready to upload!**

---

## Method 2: Manual Installation (If Library Manager Fails)

### Windows:

1. Download the library:
   - Visit: https://github.com/Cathedrow/Cryptosuite
   - Click **Code** ? **Download ZIP**

2. Extract the ZIP file

3. Move to Arduino libraries folder:
   - Open File Explorer
   - Navigate to: `C:\Users\YourUsername\Documents\Arduino\libraries`
   - (Create the `libraries` folder if it doesn't exist)
   - Paste the extracted **Crypto** folder here

4. Restart Arduino IDE completely (close and reopen)

5. Verify in **Sketch** ? **Include Library** ? you should see **Crypto**

### macOS:

- Libraries folder: `~/Documents/Arduino/libraries`
- Same steps as Windows above

### Linux:

- Libraries folder: `~/Arduino/libraries`
- Same steps as Windows above

---

## After Installation: Upload Your Code

1. Open `Nano.ino` in Arduino IDE
2. Make sure **Board** is set to: **Arduino Nano**
   - **Tools** ? **Board** ? **Arduino AVR Boards** ? **Arduino Nano**
3. Make sure **Processor** is set to: **ATmega328P**
   - **Tools** ? **Processor** ? **ATmega328P**
4. Select your **COM Port**:
   - **Tools** ? **Port** ? select your Arduino's port (e.g., COM3)
5. Click **Upload** button (or Ctrl+U)

? **Code should now compile and upload successfully!**

---

## Troubleshooting

### "Still getting HMAC.h error"

1. **Restart Arduino IDE completely** - close all windows and reopen
2. Verify installation: **Sketch** ? **Include Library** ? look for **Crypto**
3. If not there, try Method 2 (manual installation) above
4. Delete Arduino cache:
   - **File** ? **Preferences**
   - Look for "Sketchbook location" path
   - Close Arduino IDE
   - Navigate to that folder ? delete `staging` folder if it exists
   - Reopen Arduino IDE

### "Library installed but still error"

- Make sure you installed **"Crypto"** by **Tisham Dhar** (not a different library)
- Version should be **0.4.4 or newer**
- Try uninstalling and reinstalling:
  - **Sketch** ? **Include Library** ? **Manage Libraries**
  - Search for **Crypto**
  - Click your installed version ? **Uninstall**
  - Wait for it to complete
  - Search again and **Install** fresh

### "Compilation still fails after installing"

- Click **Verify** first (checkmark button) before uploading
- Check for any other errors in the compile output
- Make sure you're using the **updated Nano.ino** that includes the Crypto headers

---

## Quick Checklist ?

- [ ] Arduino IDE is open
- [ ] Installed **"Crypto"** by Tisham Dhar via Library Manager
- [ ] Restarted Arduino IDE after installation
- [ ] **Sketch** ? **Include Library** shows **Crypto** in the list
- [ ] Board is set to **Arduino Nano**
- [ ] Processor is set to **ATmega328P**
- [ ] COM Port is selected
- [ ] Clicked **Upload** button

If all checked, your code should upload successfully! ?

---

## Still Need Help?

If the Crypto library still won't install:

1. Check internet connection
2. Try a different USB cable
3. Reconnect Arduino to different USB port
4. Try on a different computer if possible
5. As a last resort, use the manual installation method above

The key is: **Arduino IDE must be able to find the Crypto library files before it can compile.**
