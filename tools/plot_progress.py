# Get user input for file path (defaults to (display windows default, using locallow environment variable))
import csv
import sys
from pathlib import Path
import pandas as pd # type: ignore
import matplotlib.pyplot as mp # type: ignore

filepath = ""
if (sys.platform == "win32"):
    fileDir = Path.home() / 'Appdata' / 'LocalLow'
elif (sys.platform == darwin):
    fileDir = Path.home() / 'Library' / 'Application Support'
else:
    fileDir = Path.home() / '.config' / 'unity3d'
fileDir = fileDir / "DefaultCompany" / "MLClassProject" / "Episode Logs"
fileName = "log.csv"
filePath = fileDir / fileName
print("Enter A log name (e.g. log.csv), full log path, or use the default path:\n" + str(filePath))
usrInput = input()
if usrInput != "":
    if (usrInput.find("/") != -1 or usrInput.find("\\") != -1):
        filePath = usrInput
    else: 
        fileName = usrInput
        filePath = fileDir / fileName

# data to be plotted
data = []
with open(filePath, "r") as f:
    csv = csv.DictReader(f)
    for row in csv:
        data.append([(row['Policy Version']), float(row["Fight Duration (s)"]), float(row["Damage Dealt"])])

df = pd.DataFrame(data, columns=["Model Iteration", "Fight Duration (s)", "Damage Dealt"])

df.plot(x="Model Iteration", y=["Fight Duration (s)", "Damage Dealt"],
        kind="line", figsize=(16, 19))
mp.title("Boss AI Model Performance")

# display plot
mp.show()




