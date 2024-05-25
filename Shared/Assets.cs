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

    public static Dictionary<string, List<string>> Frames = new Dictionary<string, List<string>>()
    {
        { 
            AssTypeEnum.Boney.ToString(), new List<string> 
            {
                "entire_ass.png",
                "chunk1.png",
                "chunk2.png",
                "chunk3.png",
                "chunk4.png",
            } 
        },

        { 
            AssTypeEnum.Cartoon.ToString(), new List<string> 
            {
                "entire_ass.png",
                "chunk1.png",
                "chunk2.png",
                "chunk3.png",
                "chunk4.png",
                "chunk5.png",
                "chunk6.png",
                "chunk7.png",
                "chunk8.png",
                "chunk9.png",
                "chunk10.png",
                "chunk11.png",
                "chunk12.png",
                "hole.png",
            } 
        },
        { 
            AssTypeEnum.Flat.ToString(), new List<string> 
            {                 
                "entire_ass.png",
                "chunk1.png",
                "chunk2.png",
                "chunk3.png",
                "chunk4.png",
                "chunk5.png",
                "chunk6.png",
                "chunk7.png",
                "chunk8.png",
                "hole.png"
            } 
        },
        { 
            AssTypeEnum.Golden.ToString(), new List<string> 
            { 
                "entire_ass.png",
                "chunk1.png",
                "chunk2.png",
                "chunk3.png",
                "chunk4.png",
                "chunk5.png",
                "chunk6.png",
                "chunk7.png",
                "chunk8.png",
                "chunk9.png",
                "chunk10.png",
                "chunk11.png",
                "chunk12.png",
                "chunk13.png",
                "chunk14.png",
                "chunk15.png",
                "chunk16.png",
                "chunk17.png",
                "chunk18.png",
                "chunk19.png",
                "chunk20.png",
                "chunk21.png",
                "chunk22.png",
                "chunk23.png",
                "chunk24.png",
                "chunk25.png",
                "hole.png",
                "hole.png",
                "hole.png",
                "hole.png",
                "hole.png"
            } 
        },
        { 
            AssTypeEnum.GYAT.ToString(), new List<string> 
            { 
                "entire_ass.png",
                "chunk1.png",
                "chunk2.png",
                "chunk3.png",
                "chunk4.png",
                "chunk5.png",
                "chunk6.png",
                "chunk7.png",
                "chunk8.png",
                "chunk9.png",
                "chunk10.png",
                "chunk11.png",
                "chunk12.png",
                "chunk13.png",
                "hole.png"

            } 
        },
        { 
            AssTypeEnum.Hairy.ToString(), new List<string> 
            { 
                "entire_ass.png",
                "chunk1.png",
                "chunk2.png",
                "chunk3.png",
                "chunk4.png",
                "chunk5.png",
                "chunk6.png",
                "chunk7.png",
                "hole.png"
            } 
        }
    };

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

    public static List<string> GetAssFrames(AssTypeEnum assType)
    {
        return Frames[assType.ToString()];
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
}
