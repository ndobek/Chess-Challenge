//Allowed Namespaces
using ChessChallenge.API;
using System; //Debug Only
//using System.Numerics;
using System.Collections.Generic;
//using System.Linq;

public class MyBot_V6 : IChessBot
{
    //Control Maps
    public ByteBoard controlMap = new ByteBoard();
    public ByteBoard whiteControlMap = new ByteBoard();
    public ByteBoard blackControlMap = new ByteBoard();


    Dictionary<string, float> positionValue = new Dictionary<string, float>();

    //Values given to various board conditions
    //float[] pieceControlValues = { 0, 10, 30, 30, 50, 90, 4, 4 }; //For when the code controlling the squares a king can move to is activated
    float[] pieceControlValues = { 0, 10, 30, 30, 50, 90, 4};
    float[] emptyControlValues = { 1, 2, 3, 4, 4, 3, 2, 1 };
    float pawnRankMod = 2;

    float turnTime;
    Timer _timer;
    bool playerIsWhite;

    public Move Think(Board board, Timer timer)
    {
        playerIsWhite = board.IsWhiteToMove;
        _timer = timer;
        turnTime = timer.MillisecondsRemaining / 50;
        if(timer.MillisecondsRemaining < 2000) turnTime = 0;
        //DEBUG_DisplayControlMaps(board);
        return MoveSort(board, 3 + board.PlyCount / 30, 3 + (int)(GetMaterialScore(board)/250), out float notUsed);
    }

    #region Search 


    public Move MoveSort(Board board, int turnsAhead, int maxSearchWidth, out float score)
    {
        Dictionary<Move, float> moveValues = new Dictionary<Move, float>();
        Move[] moves = board.GetLegalMoves();
        moveValues.Add(Move.NullMove, board.IsWhiteToMove ? int.MinValue : int.MaxValue);

        //Get Initial Value for each move
        foreach (Move move in moves)
        {

            #region Evaluate Move

            board.MakeMove(move);

            string fen = board.GetFenString();

            float moveScore;
            if (board.IsInCheckmate()) { moveScore = board.IsWhiteToMove ? int.MinValue : int.MaxValue; }//Swapped because move will be undone.
            else if (board.IsDraw()) moveScore = 0;
            else if (move.IsPromotion && move.PromotionPieceType != PieceType.Queen) moveScore = board.IsWhiteToMove ? int.MaxValue : int.MinValue;
            else if (positionValue.ContainsKey(fen)) moveScore = positionValue[fen];
            else
            {
                #region Get Control Score
                #region Build Control Map

                PieceList[] pieces = board.GetAllPieceLists();
                controlMap = new ByteBoard();
                whiteControlMap = new ByteBoard();
                blackControlMap = new ByteBoard();
                foreach (PieceList list in pieces)
                {
                    foreach (Piece piece in list)
                    {
                        if (list.IsWhitePieceList) whiteControlMap.AddBitBoard((BitboardHelper.GetPieceAttacks(piece.PieceType, piece.Square, board, piece.IsWhite)));
                        else blackControlMap.AddBitBoard(BitboardHelper.GetPieceAttacks(piece.PieceType, piece.Square, board, piece.IsWhite), -1);
                    }
                }

                if (board.IsWhiteToMove) whiteControlMap.AddBitBoard(whiteControlMap.MapOfControlledSquares());
                else blackControlMap.AddBitBoard(blackControlMap.MapOfControlledSquares(), -1);
                controlMap = whiteControlMap + blackControlMap;

                #endregion

                float moveScoreResult = 0;
                ulong kingBitBoard = BitboardHelper.GetKingAttacks(board.GetKingSquare(!board.IsWhiteToMove));
                ByteBoard playerToMoveControlMap = board.IsWhiteToMove ? whiteControlMap : blackControlMap;

                LoopBoard((int i, int j) => {
                    Piece piece = board.GetPiece(new Square(i, j));
                    ByteBoard enemyControlMap = piece.IsWhite ? blackControlMap : whiteControlMap;
                    if (enemyControlMap.GetSquareValue(piece.Square) != 0) moveScoreResult += pieceControlValues[(int)piece.PieceType] * controlMap.SquareSign(piece.Square);

                    //Controlling the squares where the king wants to move, not as helpful as i thought it would be
                    //if (BitboardHelper.SquareIsSet(kingBitBoard, piece.Square) && playerToMoveControlMap.GetSquareValue(piece.Square) != 0)
                    //{
                    //    moveScoreResult += pieceControlValues[7] * (board.IsWhiteToMove ? 1 : -1);
                    //}

                    moveScoreResult += (emptyControlValues[i] + emptyControlValues[j]) * controlMap.SquareSign(piece.Square);

                    if (piece.IsPawn)
                    {
                        if (piece.IsWhite) moveScoreResult += piece.Square.Rank * pawnRankMod;
                        else moveScoreResult -= (9 - piece.Square.Rank) * pawnRankMod;
                    }
                });

                moveScoreResult += GetMaterialScore(board);
                moveScore = moveScoreResult;
                #endregion 
            }


            board.UndoMove(move);
            positionValue[fen] = moveScore;
            moveValues.Add(move, moveScore);

            #endregion
        }

        List<Move> checkedMoves = new List<Move>
        {
            Move.NullMove
        };

        if (turnsAhead > 0)
        {
            for (int i = 0; i < maxSearchWidth; i++)
            {
                if (_timer.MillisecondsElapsedThisTurn >= turnTime && board.IsWhiteToMove != playerIsWhite) break;
                Move moveToCheck = HighestValueUncheckedMove(ref moveValues, ref checkedMoves, board);
                board.MakeMove(moveToCheck);
                float newScore;
                MoveSort(board, turnsAhead - 1, maxSearchWidth, out newScore);
                moveValues[moveToCheck] = newScore;
                board.UndoMove(moveToCheck);
            }

            checkedMoves = new List<Move>   { Move.NullMove };
        }

        Move result = HighestValueUncheckedMove(ref moveValues, ref checkedMoves, board);
        score = moveValues[result];
        return result;
    }


    #endregion

    #region Evaluate

    public float GetMaterialScore(Board board)
    {
        float result = 0;
        foreach (PieceList piece in board.GetAllPieceLists())
        {
            result += piece.Count * pieceControlValues[(int)piece.TypeOfPieceInList] * (piece.IsWhitePieceList ? 3 : -3);//An actual material gain is given 3 times as much value as controlling a piece
        }
        return result;
    }


    #endregion

    #region ByteBoard

    public class ByteBoard
    {
        public int[,] byteBoard = new int[8, 8];
        public void AddBitBoard(ulong bitBoard, int value = 1)
        {
            LoopBoard((int i, int j) => {
                byteBoard[i, j] += BitboardHelper.SquareIsSet(bitBoard, new Square(i, j)) ? value : 0;
            });
        }

        public int GetSquareValue(Square square)
        {
            return byteBoard[square.File, square.Rank];
        }
        //public static ByteBoard operator -(ByteBoard left, ByteBoard right)
        //{
        //    ByteBoard result = new ByteBoard();
        //    LoopBoard((int i, int j) => {
        //        result.byteBoard[i, j] =  (left.byteBoard[i, j] - right.byteBoard[i, j]);
        //    });
        //    return result;
        //}
        public static ByteBoard operator +(ByteBoard left, ByteBoard right)
        {
            ByteBoard result = new ByteBoard();
            LoopBoard((int i, int j) => {
                result.byteBoard[i, j] = (left.byteBoard[i, j] + right.byteBoard[i, j]);
            });
            return result;
        }
        //public int SquareSign(int file, int rank)
        //{
        //    return Math.Clamp(byteBoard[file, rank],  -1,  1);
        //}
        public int SquareSign(Square square)
        {
            return Math.Clamp(byteBoard[square.File, square.Rank], -1, 1);
        }

        public ulong MapOfControlledSquares()
        {
            ulong result = 0;
            LoopBoard((int i, int j) => {
                if (byteBoard[i, j] != 0) BitboardHelper.SetSquare(ref result, new Square(i, j));
            });
            return result;
        }

    }

    #endregion

    #region Helper Functions

    public static void LoopBoard(Action<int, int> action)
    {
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                action(i, j);
            }
        }
    }
    public static Move HighestValueUncheckedMove(ref Dictionary<Move, float> moveValues, ref List<Move> checkedMoves, Board board)
    {
        Move highestValueUncheckedMove = Move.NullMove;
        if (checkedMoves.Count < moveValues.Keys.Count)
        {
            foreach (Move move in moveValues.Keys)
            {
                if (!checkedMoves.Contains(move) &&
                    (board.IsWhiteToMove && moveValues[move] >= moveValues[highestValueUncheckedMove]) ||
                    (!board.IsWhiteToMove && moveValues[move] <= moveValues[highestValueUncheckedMove]))
                {
                    highestValueUncheckedMove = move;
                }
            }
        }
        return highestValueUncheckedMove;
    }

    #endregion

    #region Debug

    //public void DEBUG_DisplayControlMaps(Board board)
    //{
    //    BuildControlMap(board);
    //    Console.WriteLine();
    //    Console.WriteLine();
    //    Console.WriteLine("Control Map: ");
    //    for (int i = 7; i >= 0; i--)
    //    {
    //        for (int j = 0; j < 8; j++)
    //        {
    //            Console.Write($" {controlMap.byteBoard[j, i],2} ");
    //        }
    //        Console.WriteLine();
    //    }
    //    Console.WriteLine($"Control Score: {GetControlScore(board)}");
    //    Console.WriteLine();

    //}

    #endregion
}