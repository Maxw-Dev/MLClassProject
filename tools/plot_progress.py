# Get user input for file path (defaults to (display windows default, using locallow environment variable))
import csv
import sys
from pathlib import Path
import pandas as pd # type: ignore
import matplotlib.pyplot as mp # type: ignore
from os import listdir

filepath = ""
if (sys.platform == "win32"):
    fileDir = Path.home() / 'Appdata' / 'LocalLow'
elif (sys.platform == darwin):
    fileDir = Path.home() / 'Library' / 'Application Support'
else:
    fileDir = Path.home() / '.config' / 'unity3d'
fileDir = fileDir / "DefaultCompany" / "MLClassProject" / "Episode Logs"
fileList = listdir(fileDir)
fileName = "log.csv" if len(fileList) == 0 else fileList[len(fileList) - 1]
filePath = fileDir / fileName

print("Enter A log name (e.g. log.csv), full log path, or use the default path:\n(" 
    + str(filePath) 
    + ")\n" + "The following logs currently exist:")
for file in fileList:
    print(file, end = " ")
print()
usrInput = input()
if usrInput != "":
    if (usrInput.find("/") != -1 or usrInput.find("\\") != -1):
        filePath = usrInput
    else: 
        fileName = usrInput
        filePath = fileDir / fileName

# data to be plotted
data = []
playerFighter = ""
with open(filePath, "r") as f:
    lastIter = -1
    parallelIters = 0
    csv = csv.DictReader(f)
    for row in csv:
        if playerFighter == "":
            playerFighter = row["Player Fighter"]
        iterNum = row["Iteration Num"]
        if (iterNum == 0):
            parallelIters += 1
        if (lastIter != iterNum):
            if (parallelIters == 0):
                parallelIters = 1
            data.append([(iterNum * parallelIters), float(row["Fight Duration (s)"]), float(row["Damage Dealt"]), (float(row["Damage Taken"]) / 3)])
            lastIter = iterNum

df = pd.DataFrame(data, columns=["Model Iteration", "Fight Duration (s)", "Damage Dealt (%)", "Damage Taken (%)"])
fig, axes = mp.subplots(nrows=2, ncols=1)
# x="Model Iteration", y=["Fight Duration (s)", "Damage Difference (%)"],
df.plot(kind="line", figsize=(16, 9), ax=axes[0], x="Model Iteration", y=["Fight Duration (s)"])
df.plot(kind="line", figsize=(16, 9), ax=axes[1], 
        x="Model Iteration", y=["Damage Dealt (%)", "Damage Taken (%)"], 
        color=["#2acd00", "#cc1500"])
mp.suptitle("Boss AI Model Performance Against Fighter: " + playerFighter)
# display plot
mp.show()




