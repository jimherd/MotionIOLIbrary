using System;

using System.IO.Ports;

namespace MotionIOLibrary {
    public class FPGA_uP_IO {

        //***********************************************************************
        // Globals
        //***********************************************************************

        //***********************************************************************
        // Constant definitions
        //***********************************************************************

        const int COMBAUD = 256000;  // default baud rate
        const int READ_TIMEOUT = 10000;   // timeout for read reply (10 seconds)

        const int MAX_COMMAND_STRING_LENGTH = 100;
        const int MAX_REPLY_STRING_LENGTH = 100;
        const int MAX_COMMAND_PARAMETERS = 10;

        //***********************************************************************
        // Variables - GLOBAL
        //***********************************************************************

        public int nos_PWM_units = 0;
        public int nos_QE_units  = 0;
        public int nos_RC_units  = 0;

        public int SYS_REGISTERS;
        public int REGISTERS_PER_PWM_CHANNEL;
        public int REGISTERS_PER_QE_CHANNEL;
        public int RC_REGISTERS;

        public int SYS_base = 0;
        public int PWM_base = 0;
        public int QE_base  = 0;
        public int RC_base  = 0;

        //***********************************************************************
        // Variables - LOCAL
        //***********************************************************************

        //private string command_string;
        public string reply_string;
        private Int32 param_count;
        public Int32[] int_parameters = new Int32[MAX_COMMAND_PARAMETERS];
        public float[] float_parameters = new float[MAX_COMMAND_PARAMETERS];
        public string[] string_parameters = new string[MAX_COMMAND_PARAMETERS];
        private Modes[] param_type = new Modes[MAX_COMMAND_PARAMETERS];

        public enum Modes { MODE_U, MODE_I, MODE_R, MODE_S };

        public enum ErrorCode {
            NO_ERROR = 0,
            BAD_COMPORT_OPEN       = -100,
            UNKNOWN_COM_PORT       = -101,
            BAD_COMPORT_READ       = -102,
            BAD_COMPORT_WRITE      = -103,
            NULL_EMPTY_STRING      = -103,
            FPGA_NOS_UNITS_UNKNOWN = -104,
            LAST_ERROR             = -104,
        }

        //***********************************************************************
        // Objects
        //***********************************************************************

        static SerialPort _serialPort;

        //*********************************************************************
        // constructor
        //*********************************************************************
        public FPGA_uP_IO()
        {
            _serialPort = new SerialPort();    // Create a new SerialPort object

            SYS_REGISTERS               = 1;
            REGISTERS_PER_PWM_CHANNEL   = 4;
            REGISTERS_PER_QE_CHANNEL    = 7;
            RC_REGISTERS                = (3 + nos_RC_units);

    }

        //***********************************************************************
        // Methods
        //***********************************************************************
        // Init_comms : Initialise specified serial COM port
        // ==========

        public ErrorCode Init_comms(string COM_port, int baud)
        {
            ErrorCode status;

            status = ErrorCode.NO_ERROR;
            _serialPort.BaudRate = baud;
            if (string.IsNullOrEmpty(COM_port)) {
                return ErrorCode.UNKNOWN_COM_PORT;
            }
            _serialPort.PortName = COM_port;

            try {
                _serialPort.Open();
            }
            catch {
                status = ErrorCode.BAD_COMPORT_OPEN;
            }
            _serialPort.NewLine = "\n";
            return status;
        }

        //***********************************************************************
        // execute_command : format and execute a command to uP/FPGA
        // ===============
        //
        // Parameters
        //    cmd_name  char  IN   single ASCII character representing uP/FPGA command
        //    port      int   IN   return address of command
        //    register  int   IN   register address (0->255) if FPGA command
        //    in_data   int   IN   data for command  
        //    out_data  int   OUT  First piece of data returned by executed command
        //
        // Returned values
        //          status         Error value of type 'ErrorCode@
        //          out_data       int returned from FPGA 
        public FPGA_uP_IO.ErrorCode execute_command(char cmd_name, int port, int register, int in_data, out int out_data)
        {
            string command_str = cmd_name + " " + port + " " + register + " " + in_data + "\n";
            FPGA_uP_IO.ErrorCode status = (do_command(command_str, out out_data));
            return status;
        }

        //***********************************************************************
        // do_command : execute remote command and get reply
        // ==========
        //
        // Parameters
        //          command   IN   ASCII string with '\n' terminator
        //          data      OUT  First piece of data returned by executed command
        // Returned value
        //          status         Error value of type 'ErrorCode@

        public ErrorCode do_command(string command, out int data)
        {
            ErrorCode status = ErrorCode.NO_ERROR;

            status = send_command(command);
            data = 0;
            if (status != ErrorCode.NO_ERROR) {
                return status;
            }
            for (; ; ) {
                status = get_reply(ref reply_string);
                if ((reply_string[0] == 'D') && (reply_string[1] == ':')) {
                    //DebugWindow.AppendText(reply_string + Environment.NewLine);
                    continue;
                }
                else {
                    break;
                }
            }
            if (status != ErrorCode.NO_ERROR) {
                return status;
            }
            status = parse_parameter_string(reply_string);
            if (status != ErrorCode.NO_ERROR) {
                return status;
            }
            data = int_parameters[2];
            return status;
        }

        //*********************************************************************** 
        // send_command : Send string command to LLcontrol subsystem
        // ============

        public ErrorCode send_command(string command)
        {

            ErrorCode status;

            status = ErrorCode.NO_ERROR;
            try {
                _serialPort.Write(command);
            }
            catch {
                status = ErrorCode.BAD_COMPORT_WRITE;
            }
            return status;
        }

        //*********************************************************************** 
        // get_reply : Read a status/data reply string from LLcontrol subsystem
        // =========

        public ErrorCode get_reply(ref string reply)
        {

            ErrorCode status = ErrorCode.NO_ERROR;

            //serialPort1.DiscardInBuffer();
            _serialPort.ReadTimeout = READ_TIMEOUT;
            try {
                reply = _serialPort.ReadLine();
            }
            catch (TimeoutException) {
                status = ErrorCode.BAD_COMPORT_READ;
            }
            return status;
        }

        //***************************************************************************
        // parse_parameter_string : analyse string and convert into ints/floats/strings
        // ======================
        //
        // Breaks the command string into a set of token strings that are 
        // labelled REAL, INTEGER or STRING.  
        //

        public ErrorCode parse_parameter_string(string string_data)
        {
            ErrorCode status;
            Int32     index;

            status = ErrorCode.NO_ERROR;

            //
            // check string

            if (string.IsNullOrEmpty(string_data)) {
                return ErrorCode.NULL_EMPTY_STRING;
            }
            //
            //clear parameter data

            for (index = 0; index < MAX_COMMAND_PARAMETERS; index++) {
                int_parameters[index] = 0;
                float_parameters[index] = 0.0F;
                ;
                param_type[index] = Modes.MODE_U;
            }
            //
            // split string into individual strings based on SPACE separation

            string_parameters = string_data.Split(new string[] { " ", "\r", "\n" }, MAX_COMMAND_PARAMETERS, StringSplitOptions.RemoveEmptyEntries);
            param_count = string_parameters.Length;
            //
            // check each string for INTEGER or REAL values (default is STRING)

            for (index = 0; index < param_count; index++) {
                if (Int32.TryParse(string_parameters[index], out int_parameters[index]) == true) {
                    param_type[index] = Modes.MODE_I;
                    continue;
                }
                if (float.TryParse(string_parameters[index], out float_parameters[index]) == true) {
                    param_type[index] = Modes.MODE_R;
                    continue;
                }
                param_type[index] = Modes.MODE_S;
            }
            return status;
        }

        //*********************************************************************** 
        // hard_bus_check : Check FPGA and perform reset
        // ==============
        public ErrorCode hard_bus_check()
        {
            int data;

            return (do_command("Pf 0\n", out data));
        }

        //*********************************************************************** 
        // soft_bus_check : Check FPGA (no reset)
        // ==============
        public ErrorCode soft_bus_check()
        {
            int data;

            string command = "Pu 0\n";
            return (do_command(command, out data));
        }

        //*********************************************************************** 
        // get_sys_data : Read register 0 - holds data on number of I/O units
        // ============
        public ErrorCode get_sys_data()
        {
            ErrorCode status;
            int data;

            status = (do_command("r 5 0 0\n", out data));
            if (status != ErrorCode.NO_ERROR) {
                return status;
            }
            //
            // update "unit" values

            // data = int_parameters[1];
            nos_PWM_units = ((data >> 8) & 0x0F);
            nos_QE_units  = ((data >> 12) & 0x0F);
            nos_RC_units  = ((data >> 16) & 0x0F);
            //
            // update pointers to first register of each type of unit

            SYS_base = 0;
            PWM_base = SYS_base + SYS_REGISTERS;
            QE_base  = PWM_base + (nos_PWM_units * REGISTERS_PER_PWM_CHANNEL);
            RC_base  = QE_base  + (nos_QE_units  * REGISTERS_PER_QE_CHANNEL);

            return ErrorCode.NO_ERROR;
        }

    }
}
