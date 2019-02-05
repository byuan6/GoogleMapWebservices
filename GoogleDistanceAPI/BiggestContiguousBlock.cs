using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Threading.Tasks;

namespace DistanceBetween
{
    public class SingleColumn : IComparable<SingleColumn>
    {
        public object Label;
        public bool[,] AttachedArray;
        public int ColumnNum;
        public bool this[int index] { get { return this.AttachedArray[index, this.ColumnNum]; } }
        public int Length { get { return this.AttachedArray.GetLength(0); } }
        public bool? PrevNeighbor(int index)
        {
            var col = this.ColumnNum - 1;
            if (col < 0 || col >= this.AttachedArray.GetLength(1))
                return null;
            else
                return this.AttachedArray[index, col];
        }
        public bool? NextNeighbor(int index)
        {
            var col = this.ColumnNum + 1;
            if (col < 0 || col >= this.AttachedArray.GetLength(1))
                return null;
            else
                return this.AttachedArray[index, col];
        }

        public int TrueCount 
        {
            get { return Enumerable.Range(0, this.Length).Where(s=>this[s]).Count(); }
        }
        public int FalseCount
        {
            get { return Enumerable.Range(0, this.Length).Where(s => !this[s]).Count(); }
        }
        public bool Contains(bool value) { return Enumerable.Range(0, this.Length).Select(s => this[s]).Contains(value); }

        #region IComparable<SingleColumn>
        public int CompareTo(SingleColumn value)
        {
            if (this.TrueCount < value.TrueCount)
                return -1;
            else if (this.TrueCount > value.TrueCount)
                return 1;

            var len = this.Length;
            for (int i = 0; i < len; i++)
                if (!this[i] && value[i]) //!false and true, false < true, 0 < 1
                    return -1;
                else if (this[i] && !value[i]) //true and !false, true > false, 1 > 0
                    return 1;
            return 0;
        }
        #endregion IComparable<SingleColumn>
    }
    public class SingleRow : IComparable<SingleRow>
    {
        public object Label;
        public bool[,] AttachedArray;
        public int RowNum;
        public bool this[int index] { get { return this.AttachedArray[this.RowNum, index]; } }
        public int Length { get { return this.AttachedArray.GetLength(1); } }
        public bool? PrevNeighbor(int index) 
        {
            var row = this.RowNum - 1;
            if (row < 0 || row >= this.AttachedArray.GetLength(0))
                return null;
            else
                return this.AttachedArray[row, index]; 
        }
        public bool? NextNeighbor(int index)
        {
            var row = this.RowNum + 1;
            if (row < 0 || row >= this.AttachedArray.GetLength(0))
                return null;
            else
                return this.AttachedArray[row, index];
        }

        public int TrueCount
        {
            get { return Enumerable.Range(0, this.Length).Where(s => this[s]).Count(); }
        }
        public int FalseCount
        {
            get { return Enumerable.Range(0, this.Length).Where(s => !this[s]).Count(); }
        }
        public bool Contains(bool value) { return Enumerable.Range(0, this.Length).Select(s=>this[s]).Contains(value); }

        #region IComparable<SingleRow>
        public int ResetCompareCounts()
        {
            EqualCount = 0;
            LessThanCount = 0;
            GreaterThanCount = 0;
            return 0;
        }
        public int EqualCount { get; private set; }
        public int LessThanCount { get; private set; }
        public int GreaterThanCount { get; private set; }
        
        public int CompareTo(SingleRow value)
        {
            if (this.TrueCount < value.TrueCount)
                { this.LessThanCount++;  return -1; }
            else if (this.TrueCount > value.TrueCount)
                { this.GreaterThanCount++;  return 1; }

            var len = this.Length;
            for (int i = 0; i < len; i++)
                if (!this[i] && value[i]) //!false and true, false < true, 0 < 1
                { this.LessThanCount++;  return -1; }
                else if (this[i] && !value[i]) //true and !false, true > false, 1 > 0
                { this.GreaterThanCount++;  return 1; }
            
            this.EqualCount++;
            return 0;
        }
        #endregion IComparable<SingleRow>
    }
    public class ReduceSurfaceAreaByReordering
    {
        public bool[,] Area=null;

        //public Comparison<SingleRow> RowComparator;
        //public Comparison<SingleColumn> ColComparator;

        public void Sort()
        {
            if (this.Rows == null)
                this.Rows = Enumerable.Range(0, this.Area.GetLength(0)).Select(s => new SingleRow() { AttachedArray = this.Area, RowNum = s }).ToArray();

            if (this.Columns == null)
                this.Columns = Enumerable.Range(0, this.Area.GetLength(1)).Select(s => new SingleColumn() { AttachedArray = this.Area, ColumnNum = s }).ToArray();

            int[] lastrows = null;
            int[] lastcols = null;
            var rowcount = this.Area.GetLength(0);
            for (int i = 0; i < 2; i++)
            {
                this.IsAllEqual = false;

                var rows = this.Rows;
                rows.Select(s => s.ResetCompareCounts()).ToArray();
                Array.Sort<SingleRow>(rows);
                if (rows.Sum(s => s.LessThanCount + s.GreaterThanCount) == 0 && rows.Sum(s => s.EqualCount) != 0)
                {
                    this.IsAllEqual = true;
                    break; //nothing to sort, everything is equal
                }

                var cols = this.Columns;
                Array.Sort<SingleColumn>(cols);

                if (this.SortPass != null)
                    this.SortPass(this, new EventArgs());

                var sortedrows = rows.Select(s => s.RowNum).ToArray();
                var sortedcols = cols.Select(s => s.ColumnNum).ToArray();
                /*
                if (lastrows != null && lastcols!=null)
                if (sortedrows.SequenceEqual(lastrows) && sortedcols.SequenceEqual(lastcols))
                    break;
                */
                lastrows = sortedrows;
                lastcols = sortedcols;

                break;
            }
        }

        public bool this[int row, int col] 
        {
            get
            {
                var rows = this.Rows;
                var x1 = this.Columns[col].ColumnNum;
                return this.Rows[row][x1];
            }
        }

        public bool IsAllEqual { get; private set; }
        public bool IsAllTrue { get { return this.Rows.All(s=>!s.Contains(false)); } }

        //multiple pass sort until no rows or columns are moved
        //game of life problem, which is that sometimes, when this is done, there are repeating patterns that keep happening, so it can't be indefinite passes
        public SingleColumn[] Columns;
        public SingleRow[] Rows;

        public int ColumnCount { get { return this.Columns==null ? 0 : this.Columns.Length; } }
        public int RowCount { get { return this.Rows==null ? 0 : this.Rows.Length; } }

        public event EventHandler<EventArgs> SortPass;
        /*
        public Bitmap ToBitmap()
        {
            if (this.Rows==null)
                this.Rows = Enumerable.Range(0, this.Area.GetLength(0)).Select(s => new SingleRow() { AttachedArray = this.Area, RowNum = s }).ToArray();

            if(this.Columns==null)
                this.Columns = Enumerable.Range(0, this.Area.GetLength(1)).Select(s => new SingleColumn() { AttachedArray = this.Area, ColumnNum = s }).ToArray();

            var pixels = this.Area;
            var w = pixels.GetLength(1);
            var h = pixels.GetLength(0);
            var cols = this.Columns;
            var rows = this.Rows;
            Bitmap bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            Color c = Color.LightGreen;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if(this[y,x])
                        bmp.SetPixel(x, y, c);
                }
            return bmp;
        }*/
    }

    public class BiggestContiguousBlock
    {
        public BiggestContiguousBlock() { }
        public BiggestContiguousBlock(ReduceSurfaceAreaByReordering reordering)
        {
            this.Sorter = reordering;
            this.Area = reordering.Area;
        }

        public ReduceSurfaceAreaByReordering Sorter;

        public bool[,] Area = null;

        struct ChunkSolutionRect
        {
            public ChunkSolutionRect(int row, int col, int rowheight, int colwidth) 
            {
                this.Col = col;
                this.Row = row;
                this.ColWidth = colwidth;
                this.RowHeight = rowheight;
            }

            public int Col;
            public int Row;
            public int ColWidth;
            public int RowHeight;

            public int RightCol { get { return this.Col + this.ColWidth - 1; } }
            public int BottomRow { get { return this.Row + this.RowHeight - 1; } }
            public int Count { get { return this.ColWidth * this.RowHeight; } }
            public bool Exists(int col, int row)
            {
                if (this.Col >= col && this.RightCol <= col && this.Row >= row && this.BottomRow <= row)
                    return true;
                return false;
            }
            public bool HasAll(ChunkSolutionRect other)
            {
                return other.Exists(this.Row, this.Col) && other.Exists(this.Row, this.RightCol) && other.Exists(this.BottomRow, this.Col) && other.Exists(this.BottomRow, this.RightCol);
            }
            public bool IsOverlap(ChunkSolutionRect other)
            {
                if (this.Col > other.RightCol || this.RightCol < other.Col || this.Row > other.BottomRow || this.BottomRow < other.Row)
                    return false;
                return true;
            }
            public void TrimOff(ChunkSolutionRect other)
            {
                if(other.HasAll(this))
                    throw new ArgumentException("The other chunk completely encloses this one.  Trimming off intersections, will leave nothing");

                if (this.IsOverlap(other))
                {
                    //how to do a set subtraction, based simply on x,y,w,h of 2 rect...
                    //simply just find which edge intersects with this rectangle and "chop" off in direction of inside the rectangle
                    //var clearright = this.RightCol < other.Col;
                    //var clearbottom = this.BottomRow < other.Row;
                    //var cleartop = this.Row > other.BottomRow;
                    //var clearleft = this.Col > other.RightCol;

                    var topinside = this.Row < other.Row && other.Row <= this.BottomRow;
                    var bottominside = this.Row <= other.BottomRow && other.BottomRow < this.BottomRow;
                    var leftinside = this.Col < other.Col && other.Col <= this.RightCol;
                    var rightinside = this.Col <= other.RightCol && other.RightCol < this.RightCol;

                    if (leftinside) //left edge of other is inside right border, so erase everything of this, right of other's border
                    {
                        this.ColWidth = this.Col - other.Col - 1;
                    }
                    if (topinside)//top edge of other is inside bottom border, so erase everything of this, bottom of other's border
                    {
                        this.RowHeight = this.Row - other.Row - 1;
                    }
                    if (rightinside)
                    {
                        var right = this.RightCol;
                        this.ColWidth = this.RightCol - other.RightCol;
                        this.Col = other.RightCol + 1; // other.Col - this.ColWidth - 1;
                        System.Diagnostics.Debug.Assert(this.ColWidth > 0, "this is obvious, the width still has to be >0");
                        System.Diagnostics.Debug.Assert(right==this.RightCol,"these should still be the same");
                        System.Diagnostics.Debug.Assert(other.RightCol < this.Col, "the current left edge should be distinct from other's right");
                    }
                    if (bottominside)
                    {
                        var bottom = this.BottomRow;
                        this.RowHeight = this.BottomRow - other.BottomRow;
                        this.Row = other.BottomRow + 1;
                        System.Diagnostics.Debug.Assert(this.RowHeight > 0, "this is obvious, the height still has to be >0");
                        System.Diagnostics.Debug.Assert(bottom == this.BottomRow, "these should still be the same");
                        System.Diagnostics.Debug.Assert(other.BottomRow < this.Row, "the current top edge should be distinct from other's bottom");
                    }
                }
            }
            public ArraySubblock ToArraySubblock(ReduceSurfaceAreaByReordering sorter)
            {
                var translaterow = Enumerable.Range(this.Row, this.RowHeight).Select(s => sorter.Rows[s]);
                var translatecol = Enumerable.Range(this.Col, this.ColWidth).Select(s => sorter.Columns[s]);
                return new ArraySubblock(translaterow, translatecol) { Row = this.Row, Col = this.Col };
            }
            public bool IsEmpty { get { return this.ColWidth == 0 && this.RowHeight == 0; } }
            public override bool Equals(object obj)
            {
                var casted = (ChunkSolutionRect)obj;
                if (this.Row == casted.Row && this.Col == casted.Col && this.ColWidth == casted.ColWidth && this.RowHeight == casted.RowHeight)
                    return true;
                return false;
            }
            static public bool operator ==(ChunkSolutionRect a,ChunkSolutionRect b)
            {
                return a.Equals(b);
            }
            static public bool operator !=(ChunkSolutionRect a, ChunkSolutionRect b)
            {
                return !a.Equals(b);
            }
        }

        public IEnumerable<ArraySubblock> Chunkify(bool heavy)
        {
            if (this.Area == null)
                this.Area = this.Sorter.Area;
            var area = this.Area;
            var sorter = this.Sorter;

            while (true)
            {
                sorter.Sort();
                if (sorter.IsAllEqual && sorter.IsAllTrue)
                    break;

                //initialize program variables
                ChunkSolutionRect testsample = new ChunkSolutionRect() { Row = 0, Col = 0 };
                var rowcount = sorter.RowCount;
                var colcount = sorter.ColumnCount;

                if (!sorter[0, 0])
                {
                    //full row scan, skim top
                    if (tryFullRowsOfFalseOnTop(ref testsample, colcount))
                    {
                        yield return testsample.ToArraySubblock(sorter);
                    }
                    //full column scan, skim side
                    else if (tryFullColsOfFalseOnLeft(ref testsample, rowcount))
                    {
                        yield return testsample.ToArraySubblock(sorter);
                    }
                    else
                    {
                        //look for biggest block
                        testsample = getBiggestPossibleRectInUpperLeft(rowcount, colcount);
                        yield return testsample.ToArraySubblock(sorter);
                    }
                }
                else //sorter[0, 0] is true
                {
                    //algorithm below, will actually work for sorter[0, 0] is false, but the above algorithm is much easier to understand

                    //skim the top irregular shapes, bubbles in upper left
                    //Stack<ChunkSolutionRect> solutions = new Stack<ChunkSolutionRect>();
                    //ChunkSolutionRect samplemax = default(ChunkSolutionRect);
                    //var max = 0;

                    //find first bubbles in top and left, set to startcol, startrow
                    var startcol = getFirstFalseOnTopRow(colcount);
                    var startrow = getFirstFalseOnLeftCol(rowcount);

                    //run in parallel search for the first row or col with true
                    int[] rowlen=null, collen = null;
                    Parallel.For(0, 2, delegate(int branch)
                    {
                        if (branch == 1)
                        {
                            rowlen = getArrayOfFirstTrueInAllRowAfterCol(startcol, colcount, rowcount);
                        }
                        else
                        {
                            collen = getArrayOfFirstTrueInAllColumnAfterRow(startrow, colcount, rowcount);
                        }
                    });

                    //search for solutions in top bubble
                    var lastsolution = default(ChunkSolutionRect);
                    foreach(var solution in scanRowendingForSolutions(startcol, rowlen, rowcount).Reverse())
                    {
                        lastsolution = solution;
                        yield return solution.ToArraySubblock(sorter);
                    }

                    foreach(var solution in scanColendingForSolutions(startrow, collen, colcount, lastsolution, rowcount).Reverse())
                    {
                        lastsolution = solution;
                        yield return solution.ToArraySubblock(sorter);
                    }

                    //foreach (var solution in solutions.Reverse())
                    //    yield return solution.ToArraySubblock(sorter);
                }
            }
        }

        bool tryFullRowsOfFalseOnTop(ref ChunkSolutionRect testsample, int fullrowlength)
        {
            var sorter = this.Sorter;
            //full row scan, skim top
            var rowsempty = sorter.Rows.TakeWhile(s => !s.Contains(true)).Count();
            if (rowsempty != 0)
            {
                testsample.ColWidth = fullrowlength;
                testsample.RowHeight = rowsempty;
                return true;
            }
            return false;
        }

        bool tryFullColsOfFalseOnLeft(ref ChunkSolutionRect testsample, int fullcollength)
        {
            var sorter = this.Sorter;
            //full column scan, skim side
            var colsempty = sorter.Columns.TakeWhile(s => !s.Contains(true)).Count();
            if (colsempty!=0)
            {
                testsample.ColWidth = colsempty;
                testsample.RowHeight = fullcollength;
                return true;
            }
            return false;
        }

        ChunkSolutionRect getBiggestPossibleRectInUpperLeft(int rowcount, int colcount)
        {
            //this should only be called, when solution is possible at row=0, col=0
            var testsample = default(ChunkSolutionRect); 
            var sorter = this.Sorter;

            var sample = sorter.Rows.Select(s => s.RowNum).ToArray();
            Parallel.For(0, rowcount, delegate(int j)
            {
                var answer = colcount;
                for (int i = 0; i < colcount; i++)
                    if (sorter[j, i])
                    {
                        answer = i;
                        break;
                    }
                sample[j] = answer;
            });

            var runningmin = int.MaxValue;
            for (int j = 0; j < rowcount; j++)
            {
                var row = j + 1;
                var col = sample[j];
                if (col < runningmin)
                    runningmin = col;
                else if (col > runningmin)
                    col = runningmin;

                var count = col * row;
                if (count > testsample.Count)
                {
                    testsample.RowHeight = row;
                    testsample.ColWidth = col;
                }
            }
            return testsample;
        }

        int getFirstFalseOnTopRow(int colcount)
        {
            var sorter = this.Sorter;

            var startcol = 0;
            for (startcol = 0; startcol < colcount; startcol++)
                if (!sorter[0, startcol])
                    break; //it is possible for there to be blocks at top
                else
                    continue;
            return startcol;
        }
        int getFirstFalseOnLeftCol(int rowcount)
        {
            var sorter = this.Sorter;

            var startrow = 0;
            for (startrow = 0; startrow < rowcount; startrow++)
                if (!sorter[startrow, 0])
                    break; //it is possible for there to be blocks at top
                else
                    continue;
            return startrow;
        }
        int[] getArrayOfFirstTrueInAllRowAfterCol(int startcol, int colcount, int rowcount)
        {
            var sorter = this.Sorter;

            var rowlen = sorter.Rows.Select(s => s.RowNum).ToArray();
            Parallel.For(0, rowcount, delegate(int j)
            {
                var answer = colcount; //default width, if no true is found... will be offset by startcol before returning
                for (int i = startcol; i < colcount; i++)
                    if (sorter[j, i])
                    {
                        answer = i;
                        break;
                    }
                rowlen[j] = answer - startcol; //answer isn't end index, it's the width
            });
            return rowlen;
        }
        int[] getArrayOfFirstTrueInAllColumnAfterRow(int startrow, int colcount, int rowcount)
        {
            var sorter = this.Sorter;

            var collen = sorter.Columns.Select(s => s.ColumnNum).ToArray();
            Parallel.For(0, colcount, delegate(int i)
            {
                var answer = rowcount; 
                for (int j = startrow; j < rowcount; j++)
                    if (sorter[j, i])
                    {
                        answer = j;
                        break;
                    }
                collen[i] = answer - startrow; //answer isn't end index, it's the height
            });
            return collen;
        }
        IEnumerable<ChunkSolutionRect> scanRowendingForSolutions(int startcol, int[] rowend, int rowcount)
        {
            var sorter = this.Sorter;

            Stack<ChunkSolutionRect> solutions = new Stack<ChunkSolutionRect>();
            var testsample = new ChunkSolutionRect() { Col = startcol, Row=0 };
            var maxcount = 0;
            var rightedgecol = int.MaxValue;
            var samplemax = default(ChunkSolutionRect);
            for (int j = 0; j < rowcount; j++)
            {
                var colwidth = rowend[j]; // the rowend is actually a column index, or length to first true, or end of elements
                if (colwidth == 0)
                    break;
                if (colwidth < rightedgecol)
                    rightedgecol = colwidth;
                else if (colwidth > rightedgecol)
                    colwidth = rightedgecol;
                testsample.ColWidth = colwidth;
                testsample.RowHeight = j + 1;
                var count = testsample.Count; //(i - startcol) * (j - startrow + 1);
                if (count > maxcount)
                {
                    maxcount = count;
                    if (samplemax.ColWidth != testsample.ColWidth) //don't bother doing the comprehensive test, if it doesn't pass this simple one b/c unless the width of sample changes, the testsample will always be superset of prev samplemax
                        addPrevSolutionIfSubstantial(solutions, samplemax);
                    samplemax = testsample;
                    System.Diagnostics.Debug.WriteLine("max at each (row {0}) x (col {1}) = (count elements {2}) <= running max={3}, min right edge={4}", j + 1, colwidth, count, maxcount, rightedgecol);
#if DEBUG
                    for (int y = testsample.Row; y <= testsample.BottomRow; y++)
                        for (int x = testsample.Col; x <= testsample.RightCol; x++)
                            if (sorter[y, x])
                                System.Diagnostics.Debug.WriteLine(sorter[y, x], "Solution is misaligned.  THe solution elements should all be false");
#endif
                }
            }

            if (solutions.Count == 0 || samplemax != solutions.Peek())
                exclusivePush(solutions, samplemax);
            
            return solutions;
        }
        IEnumerable<ChunkSolutionRect> scanColendingForSolutions(int startrow, int[] colend, int colcount, ChunkSolutionRect lastsolution, int rowcount)
        {
            var sorter = this.Sorter;

            Stack<ChunkSolutionRect> solutions = new Stack<ChunkSolutionRect>();
            var testsample = new ChunkSolutionRect() { Col = 0, Row=startrow };
            var maxcount = 0;
            var bottomedgerow  = int.MaxValue;
            var samplemax = default(ChunkSolutionRect);

            //skim the side, search for solutions in bottom bubble
            int compensate = 0; //the column heights were searched for before solutions were found.  Now a place to store new column heights, if they were to be searched for now
            var lastsolutionbottom = lastsolution.BottomRow + 1;
            if (lastsolutionbottom > startrow)
                compensate = lastsolutionbottom - startrow; //we just store the difference between the last solution row, and the where search started
            if (startrow + compensate < rowcount)
            {
                testsample.Col = 0;
                testsample.Row = startrow + compensate; //obvious the solution has to start, forward compensated for the new solution start
                for (int i = 0; i < colcount; i++)
                {
                    var rowheight = colend[i] - compensate; //and the height has to be backward compensated for new solution start
                    if (rowheight <= 0) //backward compensation makes negative possible
                        break;
                    if (rowheight < bottomedgerow)
                        bottomedgerow = rowheight;
                    else if (rowheight > bottomedgerow)
                        rowheight = bottomedgerow;
                    testsample.ColWidth = i + 1;
                    testsample.RowHeight = rowheight;
                    var count = testsample.Count; //(i - startcol) * (j - startrow + 1);
                    if (count > maxcount)
                    {
                        maxcount = count;
                        if (samplemax.RowHeight != testsample.RowHeight) //don't bother doing the comprehensive test, if it doesn't pass this simple one b/c unless the height of sample changes, the testsample will always be superset of prev samplemax
                            addPrevSolutionIfSubstantial(solutions, samplemax);
                        samplemax = testsample;
                        System.Diagnostics.Debug.WriteLine("max at each (row {0}) x (col {1}) = (count elements {2}) <= running max={3}, min right edge={4}", rowheight, i + 1, count, maxcount, bottomedgerow);
#if DEBUG
                        for (int y = testsample.Row; y <= testsample.BottomRow; y++)
                            for (int x = testsample.Col; x <= testsample.RightCol; x++)
                                if (sorter[y, x])
                                    System.Diagnostics.Debug.WriteLine(sorter[y, x], "Solution is misaligned.  THe solution elements should all be false");
#endif
                    }
                }

                if (solutions.Count == 0 || samplemax != solutions.Peek())
                    exclusivePush(solutions, samplemax);
            }
            return solutions;
        }

        public int SubstantialBlock = 75; //100
        bool ifSubstantial(ChunkSolutionRect current, ChunkSolutionRect prev)
        {
            if (!current.IsEmpty)
            {
                var threshold = this.SubstantialBlock; //SubstantialBlock.RoundMultiply(this.Area.Length);
                var test = prev;
                test.TrimOff(prev);
                if (test.Count > threshold)
                    return true;
                else
                    return false;
            }
            else
                throw new ArgumentException("The current solution is empty, how is this a solution, much less a substantial one compared to previous");
        }
        void addPrevSolutionIfSubstantial(Stack<ChunkSolutionRect> solution, ChunkSolutionRect prev) 
        {
            if(solution==null)
                throw new ArgumentNullException("solution");

            if (!prev.IsEmpty)
            {
                if (solution.Count == 0)
                    solution.Push(prev);
                else
                {
                    var threshold = this.SubstantialBlock; //SubstantialBlock.RoundMultiply(this.Area.Length);
                    var test = prev;
                    test.TrimOff(solution.Peek());
                    if (test.Count > threshold)
                        exclusivePush(solution, prev);
                }
            }
        }
        void exclusivePush(Stack<ChunkSolutionRect> solution, ChunkSolutionRect entry)
        {
            if(!entry.IsEmpty)
                if (solution.Count == 0)
                    solution.Push(entry);
                else
                {
                    var last = solution.Pop();
                    last.TrimOff(entry); // the popped off one, AND remove common records from new set
                    entry.TrimOff(last); // now remove the common records from older one set, from latest set, to make them mutually exclusive
                    solution.Push(last);
                    solution.Push(entry);
                }
        }
    }

    public class ArraySubblock
    {
        public ArraySubblock()
        {
            this.Cols = new List<SingleColumn>();
            this.Rows = new List<SingleRow>();
        }
        public ArraySubblock(IEnumerable<SingleRow> rows, IEnumerable<SingleColumn> cols)
        {
            this.Cols = new List<SingleColumn>(cols);
            this.Rows = new List<SingleRow>(rows);
        }

        public int Col;
        public int Row;
        List<SingleColumn> Cols;
        List<SingleRow> Rows;
        public IEnumerable<int> ColIndices { get { return this.Cols.Select(s => s.ColumnNum); } }
        public IEnumerable<int> RowIndices { get { return this.Rows.Select(s => s.RowNum); } }

        public IEnumerable<ArraySubblock> Chunkify(int maxsize) //25
        {
            return Chunkify(maxsize, maxsize);
        }
        public IEnumerable<ArraySubblock> Chunkify(int rowsize, int colsize) //25x25
        {
            var rowcount = this.Rows.Count;
            var colcount = this.Cols.Count;

            var rowsegments = rowcount / rowsize;
            if (Cols.Count % rowsize != 0)
                rowsegments++;

            var colsegments = colcount / colsize;
            if (Cols.Count % colsize != 0)
                colsegments++;

            for (int r = 0; r < rowcount; r += rowsize)
                for (int c = 0; c < colcount; c += colsize)
                    yield return new ArraySubblock(
                            this.Rows.Skip(r).Take(colsize),
                            this.Cols.Skip(c).Take(rowsize)
                        ) { Row = r + this.Row, Col = c + this.Col };
        }
        public void SetAllTrue() //this must be called or Chunkify will never work
        {
            var area = this.Cols[0].AttachedArray;

            foreach (var y in this.RowIndices)
                foreach (var x in this.ColIndices)
                    if (!area[y, x])
                        area[y, x] = true;
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("This is already true (" + x + "," + y + ")");
                        System.Diagnostics.Debug.Assert(area[y, x], "This is already true (" + x + "," + y + ")");
                    }

        }
        /*
        public void DrawTo(Bitmap bmp, Color c)
        {
            var row = this.Row;
            var col = this.Col;
            var rows = this.Rows.Count;
            var cols = this.Cols.Count;
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols; i++)
                {
                    //Color c =  (i==0 || j==0 || i==cols-1 || j==rows-1) ? Color.LightBlue : Color.DarkBlue;
                    var ratio = 1;
                    var other = 3;
                    var bg = bmp.GetPixel(i + col, j + row);
                    if (i == 0 || j == 0 || i == cols - 1 || j == rows - 1)
                    {
                        ratio = 3;
                        other = 1;
                    }
                    var blended = Color.FromArgb(255, (ratio * c.R + other * bg.R) / 4, (ratio * c.G + other * bg.G) / 4, (ratio * c.B + other * bg.B) / 4);
                    bmp.SetPixel(i + col, j + row, blended);
                }
        }*/
    }
}
