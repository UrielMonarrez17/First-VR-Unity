**Team**   
Uriel Arturo Monarrez Cervantes  
[Gerardo Esquivel Sosa](mailto:0258827@up.edu.mx)  
[Juan Pablo Villaseñor Navares](mailto:0256907@up.edu.mx)  
**AI Declaration**   
We use AI as an interactive technical assistant and pair-programming thought partner during the development and troubleshooting of this Unity 6 Meta Quest 3 VR project.

All final implementation decisions, testing, and deployment steps were executed and verified independently in Unity 6000.0.37f1 and on the Meta Quest 3 hardware.

### **AI Tools Used**

**Primary Tool:** Gemini 2.5 (Google)

**Scope of Use:** Technical setup guidance, Unity 6 API migration/adaptation, troubleshooting hardware deployment (ADB/USB connection errors), and structuring hand-tracking interaction workflows.

### **Log of Relevant Prompts & AI Responses**

| Prompts | AI Output & Purpose |
| :---- | :---- |
| How do I create my first VR program in unity for the meta quest 3 | Provided initial general setup instructions for Unity 3D URP, XR Interaction Toolkit (XRI), and Meta Quest Developer Mode. |
| Consider I want to manipulate a cube with the hands | Outlined hand tracking steps using XRI Direct Interactor, setting up an XR Grab Interactable Cube with Kinematic physics, and gesture configuration. |
| All of this works for unity 6000.5.7? | Highlighted breaking changes and workflow shifts specific to Unity 6000.5.7 |

**Implementation**  
For the project in Unity 6, we set up the whole scene in 3D URP so it would run natively on the Meta Quest 3 by switching the build platform to Android. We installed the XR Interaction Toolkit along with XR Hands, turned on OpenXR, and configured the hand tracking subsystem in the project settings so it would track my hands without needing physical controllers. Inside the scene, we threw in the XR Origin rig with direct interactors and added an XR Grab Interactable component to a cube, setting its Rigidbody. Once everything checked out and Developer Mode was enabled on the Quest, we plugged it in via USB-C and hit Build and Run to compile the APK directly onto the headset and test it out live. 