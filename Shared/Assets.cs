using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazorApp.Shared;

public class Assets
{
    public enum AssTypeEnum
    {
        Boney,
        Cartoon,
        Flat,
        Golden,
        GYAT,
        Hairy
    }

    public static List<AssTypeEnum> AssTypes = new()
    {
        AssTypeEnum.Boney,
        AssTypeEnum.Cartoon,
        AssTypeEnum.Flat,
        AssTypeEnum.Golden,
        AssTypeEnum.GYAT,
        AssTypeEnum.Hairy
    };

    public static Dictionary<string, int> ClicksRequired = new()
    {
        { AssTypeEnum.Boney.ToString(),   4  },
        { AssTypeEnum.Cartoon.ToString(), 13 },
        { AssTypeEnum.Flat.ToString(),    9  },
        { AssTypeEnum.Golden.ToString(),  29 },
        { AssTypeEnum.GYAT.ToString(),    14 },
        { AssTypeEnum.Hairy.ToString(),   8  },
    };

    public static bool HasHole(AssTypeEnum assType) => assType != AssTypeEnum.Boney;

    public static AssTypeEnum GetRandomAssType()
    {
        Random random = new Random();

        // Create a weighted list of enum values
        var weightedValues = new[]
        {
            AssTypeEnum.Boney,
            AssTypeEnum.Cartoon,
            AssTypeEnum.Flat,
            AssTypeEnum.GYAT,
            AssTypeEnum.Hairy
        };

        // Add 99 normal values and 1 'Golden' to make it 1% chance for 'Golden'
        var weightedList = new AssTypeEnum[50];
        for (int i = 0; i < 49; i++)
        {
            weightedList[i] = weightedValues[random.Next(weightedValues.Length)]; // exclude 'Golden'
        }
        weightedList[49] = AssTypeEnum.Golden; // 1% chance

        return weightedList[random.Next(weightedList.Length)];
    }

    public static double GetPointsForAssType(AssTypeEnum assType)
    {
        switch (assType)
        {
            case AssTypeEnum.Boney:
                return 0.5;
            case AssTypeEnum.Golden:
                return 10;
            case AssTypeEnum.GYAT:
                return 2;
            case AssTypeEnum.Cartoon:
            case AssTypeEnum.Flat:
            case AssTypeEnum.Hairy:
            default:
                return 1;
        }
    }

    public static string GetBiteSoundForAssType(AssTypeEnum assType)
    {
        var baseDirectory = "sounds/Bites";
        var sound = assType switch
        {
            AssTypeEnum.Boney => "light_crunch.mp3",
            AssTypeEnum.Golden => "metal_crunch.mp3",
            _ => "bite.mp3",
        };

        return $"/{baseDirectory}/{sound}";
    }

    public static string GetRandomGameOverSound()
    {
        List<string> gameOverSounds = new()
        {
            "burp1.mp3",
            "burp2.mp3",
            "satisfied.mp3"
        };

        Random random = new();

        var randomIdx = random.Next(gameOverSounds.Count());

        string baseDirectory = "sounds/TimeUp";
        return $"/{baseDirectory}/{gameOverSounds[randomIdx]}";
    }
}
