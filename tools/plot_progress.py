# Get user input for file path (defaults to (display windows default, using locallow environment variable))
import os
import csv
import pandas as pd # type: ignore
import matplotlib.pyplot as mp # type: ignore

filepath = os.getenv('LOCALAPPDATA') + "\\..\\LocalLow\\DefaultCompany\\MLClassProject\\Episode Logs\\log.csv"
print("Enter A path for the log or use the default: " + filepath)
usrInput = input()
if usrInput != "":
    filepath = usrInput

# data to be plotted
data = []
with open(filepath, "r") as f:
    csv = csv.DictReader(f)
    for row in csv:
        data.append([(row['Policy Version']), float(row["Fight Duration (s)"]), float(row["Damage Dealt"])])

df = pd.DataFrame(data, columns=["Model Iteration", "Fight Duration (s)", "Damage Dealt"])

df.plot(x="Model Iteration", y=["Fight Duration (s)", "Damage Dealt"],
        kind="line", figsize=(16, 19))
mp.title("Boss AI Model Performance")

# display plot
mp.show()




