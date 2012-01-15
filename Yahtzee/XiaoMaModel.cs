using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

using Microsoft.Modeling;


[assembly: Microsoft.Xrt.Runtime.NativeType("Yahtzee.Model.ScoreType")]
//[assembly: Microsoft.Xrt.Runtime.NativeType("System.Int32[]")]
namespace Yahtzee.Model
{
    static class ModelProgram
    {
        #region Parameters & flags

        static int UPPER_BONUS = 35;
        static int YAHTZEE_BONUS = 100;

        static Die[] dice = new Die[5];
        static bool inGame = false; //All actions, except NewGame(), can not be invoked before game begin.
        static bool roundBegin = true;//indicate it is a new round before 1st roll.
        static int rollTimes;
        static int totalScore;
        static int leftForBonus;
        static int[] scoreSection = new int[13];//A list storing score for each section. All values should be set to -1 as game begin. 

        #endregion

        #region Helper Methods

        static void NewRound()
        {
            rollTimes = 0;

            for (int i = 0; i < 5; i++)
            {
                dice[i].value = 0;
                dice[i].onHold = false;
            }

            roundBegin = true;
        }

        //check if it's Yahtzee
        static bool YahtzeeCheck()
        {
            if (dice[0].value == dice[1].value && 
                dice[0].value == dice[2].value &&
                dice[0].value == dice[3].value &&
                dice[0].value == dice[4].value)
                return true;

            return false;
        }

        #endregion

        #region Rule Methods

        [Rule]
        static void NewGame()
        {
            for (int i = 0; i < 13; i++)
                scoreSection[i] = -1;

            totalScore = 0;
            leftForBonus = 63;
            inGame = true;

            NewRound();
        }

        /* try to use int[] as input. But the tool does not allow it,
         * since it is not a primitive type. Haven't figure out how to
         * add it as a NativeType. 
       [Rule]
       static bool RollAll(int[] values)
       {
           //checks if it is in the game
           Condition.IsTrue(inGame);
           Condition.IsTrue(rollTimes < 4);
                         
           for (int i = 0; i < 5; i++)
           {
               if (dice[i].onHold == false)
                   dice[i].value = values[i];                              
           }

           rollTimes++;
           roundBegin = false;
                
           return true;
       }*/


        [Rule]
        static bool RollAll(int i1, int i2, int i3, int i4, int i5)
        {
            //checks if it is in the game
            Condition.IsTrue(inGame);

            //max roll times is 3
            Condition.IsTrue(rollTimes < 3);

            Condition.IsTrue(i1 > 0 && i1 < 7);
            Condition.IsTrue(i2 > 0 && i2 < 7);
            Condition.IsTrue(i3 > 0 && i3 < 7);
            Condition.IsTrue(i4 > 0 && i4 < 7);
            Condition.IsTrue(i5 > 0 && i5 < 7);

            //Avoid holding all dices at the same time.
            Condition.IsFalse(dice[0].onHold && dice[1].onHold && dice[2].onHold && dice[3].onHold && dice[4].onHold);

            dice[0].value = i1;
            dice[1].value = i2;
            dice[2].value = i3;
            dice[3].value = i4;
            dice[4].value = i5;

            rollTimes++;
            roundBegin = false;

            return true;
        }

        //This action can be invoked multiple times for the same dice 
        //in one round to set the OnHold state on and off.
        [Rule]
        static void ToggleOnHold(int i)
        {
            //checks if it is in the game
            Condition.IsTrue(inGame);

            //i means ith die
            Condition.IsTrue(i > 0 && i < 6);

            //dice could only be held after 1st roll.
            Condition.IsFalse(roundBegin);

            //should not be invoked after 3rd roll
            Condition.IsTrue(rollTimes < 3);

            if (dice[i - 1].onHold == false)
                dice[i - 1].onHold = true;
            else
                dice[i - 1].onHold = false;
        }

        //This rule is based on the assumption that the player 
        //has the option to choose any score section he want to 
        //socre, provided only that it is not already scored. 
        //Therefore, the score in certain round could be 0, even if 
        //the result could be used to score for more points.
        //The return value of this action is the total score at the time.
        [Rule]
        static int Score(ScoreType scoreType)
        {
            int scoreValue = 0;
            int type = (int)scoreType;

            //checks if it is in the game
            Condition.IsTrue(inGame);

            //score could only be invoked after ist roll.
            Condition.IsFalse(roundBegin);

            //check if the section is already scored.
            Condition.IsTrue(scoreSection[type] < 0);

            //wild card rule check, see if the corresponding upper section and Yahtzee section
            //are scored before using Yahtzee as wild cards for small/large straight.
            //Besides, scoring Yahtzee in other lower sections(3 of a kind, full house, etc) 
            //instead of upper section is considered as a legal move.
            Condition.IsFalse(YahtzeeCheck() && (type == 9 || type == 10) &&
                ((scoreSection[dice[0].value - 1] == -1) || (scoreSection[12] == -1)));


            /*
             * If the first Yahtzee must be scored in Yahtzee section
             * Condition.IsFalse(YahtzeeCheck() && (type != 12) && (scoreSection[12] == -1))    
             * 
             * If the wild card can only be used to score in lower sections
             * after corresponding upper section was already taken
             * Condition.IsFalse(YahtzeeCheck() && (type == 6 || type == 7 || type == 8 || type == 9 || type == 10 || type == 11) 
                && ((scoreSection[dice[0].value - 1] == -1) || (scoreSection[12] == -1)));
             */

            //score for Yahtzee bonus
            if (scoreSection[12] == 50)
                if (YahtzeeCheck())
                    totalScore += YAHTZEE_BONUS;


            //score for upper section
            if (0 <= type && type <= 5)
            {
                for (int i = 0; i < 5; i++)
                {
                    if (dice[i].value == (type + 1))
                    {
                        scoreValue += dice[i].value;
                    }
                }
                
                //check for upper bonus
                leftForBonus -= scoreValue;                
                if (leftForBonus <= 0)
                {
                    totalScore += UPPER_BONUS;

                    leftForBonus = 255; //set a big enough number to leftForBonus to avoid add bonus for more than one time.
                }
            }

            //score for three-of-a-kind
            if (type == 6)
            {
                for (int i = 1; i < 7; i++)
                {
                        int times = 0;

                        for (int j = 0; j < 5; j++)
                        {
                            if (dice[j].value == i)
                                times++;
                        }

                        if (times >= 3)
                        {
                            for (int k = 0; k < 5; k++)
                                scoreValue += dice[k].value;
                        }
                }
            }

            //score for four-of-a-kind
            if (type == 7)
            {
                for (int i = 1; i < 7; i++)
                {
                    int times = 0;

                    for (int j = 0; j < 5; j++)
                    {
                        if (dice[j].value == i)
                            times++;
                    }

                    if (times >= 4)
                    {
                        for (int k = 0; k < 5; k++)
                            scoreValue += dice[k].value;
                    }
                }
            }

            //score for full house
            if (type == 8)
            {
                bool triple = false;
                bool pair = false;

                for (int i = 1; i < 7; i++)
                {
                    int times = 0;

                    for (int j = 0; j < 5; j++)
                    {
                        if (dice[j].value == i)
                            times++;
                    }

                    if (times == 3)
                        triple = true;
                    else if (times == 2)
                        pair = true;
                    else if (times == 5)//if it's a Yahtzee
                    {
                        pair = true;
                        triple = true; 
                    }                       
                }

                if (triple && pair)
                    scoreValue = 25;
            }

            //score for small straight
            if (type == 9)
            {
                //a boolean list represents value 1 to 6;
                bool[] valueList = new bool[6];
                for (int i = 0; i < 6; i++)
                    valueList[i] = false;

                //set valueList[i] to true if the  corresponding number is rolled
                for (int i = 0; i < 5; i++)
                    valueList[dice[i].value - 1] = true;

                //To be successfully scored as small straight,
                //3 and 4 must be rolled.
                //Other dice could be 1 & 2, 2 & 5, and 4 & 5.
                if (valueList[2] && valueList[3])
                    if ((valueList[0] && valueList[1]) || (valueList[1] && valueList[4]) || (valueList[4] && valueList[5]))
                        scoreValue = 30;

                //Using Yahtzee as wild card
                if (YahtzeeCheck())
                    scoreValue = 30;
            }

            //score for large straight
            if (type == 10)
            {
                bool pair = false;
                bool hasOne = false;
                bool hasSix = false;

                for (int i = 1; i < 7; i++)
                {
                    int times = 0;

                    for (int j = 0; j < 5; j++)
                    {
                        if (dice[j].value == i)
                            times++;
                    }

                    if (times >= 2)
                    {
                        pair = true;
                    }
                }

                //if there is no pair and no 1 or 6 in the result, it must be a large straight.
                if (!pair && (!hasOne || !hasSix))
                    scoreValue = 40;

                //Using Yahtzee as wild card
                if (YahtzeeCheck())
                    scoreValue = 40;
            }

            //score for chance
            if (type == 11)
            {
                for (int i = 0; i < 5; i++)
                    scoreValue += dice[i].value;
            }

            //score for Yahtzee
            if (type == 12)
            {
                if (YahtzeeCheck())
                    scoreValue = 50;
            }

            scoreSection[type] = scoreValue;
            totalScore += scoreValue;

            //if all section were scored, the game end.
            for (int i = 0; i < 13; i++)
            {
                if (scoreSection[i] < 0)
                {
                    NewRound(); 
                    return totalScore;
                }              
            }
            inGame = false;

            return totalScore;
        }

        #endregion
    }

    #region Custom Types

    public struct Die
    {
        public int value;
        public bool onHold;
    }

    public enum ScoreType
    { 
        One,
        Two,
        Three,
        Four,
        Five,
        Six,
        ThreeOfAKind,
        FourOfAKind,
        FullHouse,
        SmallStraight,
        LargeStraight,
        Chance,
        Yahtzee
    }

    #endregion
}
