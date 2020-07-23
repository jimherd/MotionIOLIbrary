using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO.Ports;

namespace MotionIOLibrary
{
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
        const int MAX_REPLY_STRING_LENGTH   = 100;
        const int MAX_COMMAND_PARAMETERS    = 10;

        //***********************************************************************
        // Variables - GLOBAL
        //***********************************************************************

        public int nos_PWM_channels = -1;
        public int nos_QE_channels  = -1;
        public int nos_RC_channels  = -1;

        public uint PWM_base = 1;
        public uint QE_base  = 0;
        public uint RC_base  = 0;

        //***********************************************************************
        // Variables - LOCAL
        //***********************************************************************

        private string command_string;
        private string reply_string;
        private Int32  param_count;
        private  Int32[] int32_parameters   = new Int32[MAX_COMMAND_PARAMETERS];
        private  float[] float_parameters   = new float[MAX_COMMAND_PARAMETERS];
        private string[] string_parameters  = new string[MAX_COMMAND_PARAMETERS];
        private Modes[] param_type          = new Modes[MAX_COMMAND_PARAMETERS];

        

        public enum  Modes { MODE_U, MODE_I, MODE_R, MODE_S };

        public enum ErrorCode {
            NO_ERROR            = 0,
            BAD_COMPORT_OPEN = -100,
            UNKNOWN_COM_PORT = -101,
            BAD_COMPORT_READ = -102,
            BAD_COMPORT_WRITE = -103,
           
        }

        //***********************************************************************
        // Objects
        //***********************************************************************

        static SerialPort _serialPort;

        //*********************************************************************
        // constructor
        //*********************************************************************
        public FPGA_uP_IO() {
            
            _serialPort = new SerialPort(); // Create a new SerialPort object
        }

        //***********************************************************************
        // Methods
        //***********************************************************************
        // Init_comms : Initialise specified serial COM port
        // ==========

        public ErrorCode Init_comms(string COM_port, int baud) {
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
            return status;
        }

        //***********************************************************************
        // do_command : execute remote command and get reply
        // ==========
        public ErrorCode do_command(string command) {

            ErrorCode status = ErrorCode.NO_ERROR;

            status = send_command(command_string);
            if (status != ErrorCode.NO_ERROR) {
                return  status;
            }
            status = get_reply(reply_string);
            if (status != ErrorCode.NO_ERROR) {
                return status;
            }
            status = parse_parameter_string(reply_string);
            if (status != ErrorCode.NO_ERROR) {
                return status;
            }
            return status;
        }

        //*********************************************************************** 
        // send_command : Send string command to LLcontrol subsystem
        // ============
        //
        public ErrorCode send_command(string command) {

            ErrorCode status;

            status = ErrorCode.NO_ERROR;
            try {
                _serialPort.WriteLine(command);
            } 
            catch {
                status = ErrorCode.BAD_COMPORT_WRITE;
            }
            return status;
        }

        //*********************************************************************** 
        // get_reply : Read a status/data reply from LLcontrol subsystem
        // =========

        public ErrorCode get_reply(string reply) {

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

        public ErrorCode parse_parameter_string(string string_data) {

            ErrorCode status;

            status = ErrorCode.NO_ERROR;
            //
            //clear parameter data
            //
            for (int i= 0; i < MAX_COMMAND_PARAMETERS; i++) {
                int32_parameters[i]  = 0;
                float_parameters[i]  = 0.0F;
                string_parameters[i] = "";
                param_type[i]        = Modes.MODE_U;
            }
        //
        // split string into individual strings based on SPACE separation
        //*
            string_parameters = string_data.Split(new string[] { " ", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            param_count = string_parameters.Length;
        //
        // check each string for integer or real values (default is STRING)
        //
            for (int i=0; i < param_count; i++) {
                if (Int32.TryParse(string_parameters[i], out int32_parameters[i])  == true) {
                    param_type[i] = Modes.MODE_I;
                    continue;
                }
                if (float.TryParse(string_parameters[i], out float_parameters[i])  == true) {
                    param_type[i] = Modes.MODE_R;
                    continue;
                }
                param_type[i] = Modes.MODE_S;
            }
            return status;
        }
    }
}
