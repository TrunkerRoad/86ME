﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Windows.Forms;
using System.IO;

namespace _86ME_ver1
{
    enum mtest_method { always, keyboard, bluetooth, ps2, acc };
    enum keyboard_method { first, pressed, release };
    enum auto_method { on, off, title };
    enum serial_ports { serial1, serial2, serial3 };
    enum motion_property { blocking, nonblocking };
    enum internal_trigger { call, jump };

    class FSMGen
    {
        private string nfilename = "";
        private ArrayList ME_Motionlist;
        private NewMotion Motion;
        private bool[] method_flag = new bool[16];
        private int[] offset = new int[45];
        private string[] ps2_pins = new string[4];
        private string bt_baud;
        private string bt_port;
        private int opVar_num = 50;
        private bool IMU_compensatory = false;
        private Quaternion invQ = new Quaternion();

        public FSMGen(NewMotion nMotion, int[] off, ArrayList motionlist, string[] ps2pins, string bt_baud, string bt_port)
        {
            this.Motion = nMotion;
            this.offset = off;
            this.ME_Motionlist = motionlist;
            this.ps2_pins = ps2pins;
            this.bt_baud = bt_baud;
            this.bt_port = bt_port;
            this.invQ.w = double.Parse(Motion.maskedTextBox1.Text);
            this.invQ.x = double.Parse(Motion.maskedTextBox2.Text);
            this.invQ.y = double.Parse(Motion.maskedTextBox3.Text);
            this.invQ.z = double.Parse(Motion.maskedTextBox4.Text);
            this.invQ = this.invQ.Normalized().Inverse();
            for (int i = 0; i < ME_Motionlist.Count; i++)
            {
                int method = ((ME_Motion)ME_Motionlist[i]).trigger_method;
                method_flag[method] = true;
            }
            if (Motion.comboBox2.SelectedIndex != 0)
            {
                method_flag[4] = true;
                for (int i = 0; i < 45; i++ )
                {
                    if (Motion.fbox2[i].SelectedIndex != 2 && Motion.p_gain[i] != 0)
                        IMU_compensatory = true;
                }
            }
        }

        private int convert_keynum(int keynum)
        {
            if (keynum <= 25)
                return keynum + 4;
            else if (keynum == 26)
                return 0x2C;
            else if (keynum == 27)
                return 0x50;
            else if (keynum == 28)
                return 0x4F;
            else if (keynum == 29)
                return 0x52;
            else if (keynum == 30)
                return 0x51;
            else if (keynum == 31)
                return 0x29;
            else
                return 0;
        }

        private string trigger_condition(ME_Motion m)
        {
            switch (m.trigger_method)
            {
                case (int)mtest_method.always:
                    if (m.auto_method == (int)auto_method.on)
                        return "1";
                    else if (m.auto_method == (int)auto_method.off)
                        return "0";
                    else //title
                        return m.name + "_title == 1";
                case (int)mtest_method.keyboard:
                    if (m.trigger_keyType == (int)keyboard_method.first)
                        return "key_state(" + convert_keynum(m.trigger_key) + ") == 2";
                    else if (m.trigger_keyType == (int)keyboard_method.pressed)
                        return "key_state(" + convert_keynum(m.trigger_key) + ") == 3";
                    else // release
                        return "key_state(" + convert_keynum(m.trigger_key) + ") == 1";
                case (int)mtest_method.bluetooth:
                    if (String.Compare("", m.bt_key) == 0)
                        return "0";
                    else
                        return bt_port + "_Command == \'" + m.bt_key + "\'";
                case (int)mtest_method.ps2:
                    if (m.ps2_type == (int)keyboard_method.first)
                        return "ps2x.ButtonPressed(" + m.ps2_key + ")";
                    else if (m.ps2_type == (int)keyboard_method.pressed)
                        return "ps2x.Button(" + m.ps2_key + ") && !ps2x.ButtonPressed(" + m.ps2_key + ")";
                    else
                        return "ps2x.ButtonReleased(" + m.ps2_key + ")";
                case (int)mtest_method.acc:
                    return m.name + "::acc_state == 2";
                default:
                    return "1";
            }
        }

        private string op2str(int left, int val1, int val2, int n, int form)
        {
            switch (form)
            {
                case 0:
                    if (n == 0) return "    " + var2str(left) + " = " + var2str(val1) + "+" + var2str(val2) + ";";
                    else if (n == 1) return "    " + var2str(left) + " = " + var2str(val1) + "-" + var2str(val2) + ";";
                    else if (n == 2) return "    " + var2str(left) + " = " + var2str(val1) + "*" + var2str(val2) + ";";
                    else if (n == 3) return "    if(" + var2str(val2) + " != 0) " + var2str(left) + " = " + var2str(val1) +
                                            "/" + var2str(val2) + ";\n    else " + var2str(left) + " = 0;";
                    else if (n == 4) return "    " + var2str(left) + " = pow(" + var2str(val1) + ", " + var2str(val2) + ");";
                    else if (n == 5) return "    if(" + var2str(val2) + " != 0) " + var2str(left) + " = fmod(" + var2str(val1) +
                                            ", " + var2str(val2) + ");\n    else " + var2str(left) + " = 0;";
                    else if (n == 6) return "    " + var2str(left) + " = (int)" + var2str(val1) + " & (int)" + var2str(val2) + ";";
                    else if (n == 7) return "    " + var2str(left) + " = (int)" + var2str(val1) + " | (int)" + var2str(val2) + ";";
                    break;
                case 1:
                    if (n == 0) return "    " + var2str(left) + " = ~((int)" + var2str(val2) + ");";
                    else if (n == 1) return "    " + var2str(left) + " = sqrt(" + var2str(val2) + ");";
                    else if (n == 2) return "    " + var2str(left) + " = exp(" + var2str(val2) + ");";
                    else if (n == 3) return "    if(" + var2str(val2) + " != 0) " + var2str(left) + " = log(" + var2str(val2) + ");\n" +
                                            "    else " + var2str(val2) + " = 0;";
                    else if (n == 4) return "    if(" + var2str(val2) + " != 0) " + var2str(left) + " = log10(" + var2str(val2) + ");\n" +
                                            "    else " + var2str(val2) + " = 0;";
                    else if (n == 5) return "    " + var2str(left) + " = fabs(" + var2str(val2) + ");";
                    else if (n == 6) return "    " + var2str(left) + " = -" + var2str(val2) + ";";
                    else if (n == 7) return "    " + var2str(left) + " = cos(" + var2str(val2) + ");";
                    else if (n == 8) return "    " + var2str(left) + " = sin(" + var2str(val2) + ");";
                    break;
            }
            return "";
        }

        private string method2str(int n)
        {
            switch(n)
            {
                case 0:
                    return "==";
                case 1:
                    return "!=";
                case 2:
                    return ">=";
                case 3:
                    return "<=";
                case 4:
                    return ">";
                case 5:
                    return "<";
                default:
                    return "==";
            }
        }

        private string var2str(int n)
        {
            if (n == opVar_num)
                return "millis()";
            else if (n == opVar_num + 1)
                return "rand()";
            else if (n < opVar_num + 8 && n >= opVar_num + 2)
                return "analogRead(" + (n - opVar_num - 2) + ")";
            else if (n < opVar_num + 11 && n >= opVar_num + 8)
                return "_IMU_val[" + (n - opVar_num - 8) + "]";
            else if (n == opVar_num + 11)
                return "_roll";
            else if (n == opVar_num + 12)
                return "_pitch";
            else
                return "_86ME_var[" + n + "]";
        }

        private string set_space(int n)
        {
            string ret_str = "";
            for (int i = 0; i < n; i++)
                ret_str += " ";
            return ret_str;
        }

        private void generate_variable(ME_Motion m, TextWriter writer, List<int> channels)
        {
            int space_num = 2;
            string space = set_space(space_num);
            writer.Write("namespace " + m.name + "\n{\n" + space + "enum {IDLE");
            if (m.Events.Count > 0)
                writer.Write(", ");
            m.goto_var.Clear();
            m.states.Clear();
            m.states.Add("IDLE");
            for (int i = 0; i < m.Events.Count; i++)
            {
                if(m.Events[i] is ME_Frame)
                {
                    writer.Write("FRAME_" + i.ToString() + ", " + "WAIT_FRAME_" + i.ToString());
                    m.states.Add("FRAME_" + i.ToString());
                    m.states.Add("WAIT_FRAME_" + i.ToString());
                    if (i != m.Events.Count - 1)
                        writer.Write(", ");
                }
                else if (m.Events[i] is ME_Delay)
                {
                    writer.Write("DELAY_" + i.ToString() + ", " + "WAIT_DELAY_" + i.ToString());
                    m.states.Add("DELAY_" + i.ToString());
                    m.states.Add("WAIT_DELAY_" + i.ToString());
                    if (i != m.Events.Count - 1)
                        writer.Write(", ");
                }
                else if (m.Events[i] is ME_Flag)
                {
                    writer.Write("FLAG_" + i.ToString());
                    m.states.Add("FLAG_" + i.ToString());
                    if (i != m.Events.Count - 1)
                        writer.Write(", ");
                }
                else if (m.Events[i] is ME_Goto)
                {
                    writer.Write("GOTO_" + i.ToString());
                    m.states.Add("GOTO_" + i.ToString());
                    if (i != m.Events.Count - 1)
                        writer.Write(", ");
                    for (int k = 0; k < m.Events.Count; k++)
                    {
                        if (m.Events[k] is ME_Flag)
                        {
                            if (String.Compare(((ME_Goto)m.Events[i]).name, ((ME_Flag)m.Events[k]).name) == 0)
                            {
                                ME_Goto g = (ME_Goto)m.Events[i];
                                ME_Flag f = (ME_Flag)m.Events[k];
                                if (g.is_goto && g.infinite == false)
                                {
                                    f.var = g.name + "_" + i;
                                    m.goto_var.Add(g.name + "_" + i);
                                    break; // match once
                                }
                            }
                        }
                    }
                }
                else if (m.Events[i] is ME_Trigger)
                {
                    writer.Write("MOTION_" + i.ToString() + ", " + "WAIT_MOTION_" + i.ToString());
                    m.states.Add("MOTION_" + i.ToString());
                    m.states.Add("WAIT_MOTION_" + i.ToString());
                    if (i != m.Events.Count - 1)
                        writer.Write(", ");
                }
                else if (m.Events[i] is ME_Release)
                {
                    writer.Write("RELEASE_" + i.ToString());
                    m.states.Add("RELEASE_" + i.ToString());
                    if (i != m.Events.Count - 1)
                        writer.Write(", ");
                }
                else if (m.Events[i] is ME_If)
                {
                    writer.Write("IF_" + i.ToString());
                    m.states.Add("IF_" + i.ToString());
                    if (i != m.Events.Count - 1)
                        writer.Write(", ");
                }
                else if (m.Events[i] is ME_Compute)
                {
                    writer.Write("COMPUTE_" + i.ToString());
                    m.states.Add("COMPUTE_" + i.ToString());
                    if (i != m.Events.Count - 1)
                        writer.Write(", ");
                }
            }
            writer.WriteLine("};");
            writer.WriteLine(space + "int state = IDLE;");
            writer.WriteLine(space + "unsigned long time;");
            for (int i = 0; i < m.goto_var.Count; i++)
                writer.WriteLine(space + "int " + m.goto_var[i] + " = 0;");
            if (m.moton_layer == 1)
            {
                writer.Write("  int mask[" + channels.Count + "] = { ");
                for (int j = 0; j < channels.Count; j++)
                {
                    if (m.used_servos.Contains(channels[j]))
                        writer.Write("0xffff");
                    else
                        writer.Write("0");
                    if (j != channels.Count - 1)
                        writer.Write(", ");
                }
                writer.WriteLine("};");
            }
            if (m.trigger_method == (int)mtest_method.acc)
            {
                writer.WriteLine("  int acc_state = 0;");
                writer.WriteLine("  unsigned long acc_time;");
            }
            writer.WriteLine("}");
        }

        private void generate_updateIMU(TextWriter writer, List<int> channels)
        {
            if (method_flag[4])
            {
                writer.WriteLine("void updateIMU()\n{");
                writer.WriteLine("  if(millis() - _IMU_update_time < 20 && _IMU_init_status != 0) return;\n"); // ~50fps
                writer.WriteLine("  _IMU.getQ(_IMU_Q, _IMU_val);");
                writer.WriteLine("  double _w, _x, _y, _z;");
                writer.WriteLine("  _w = _IMU_Q[0]*" + invQ.w + " - _IMU_Q[1]*" + invQ.x +
                                    " - _IMU_Q[2]*" + invQ.y + " - _IMU_Q[3]*" + invQ.z + ";");
                writer.WriteLine("  _x = _IMU_Q[1]*" + invQ.w + " + _IMU_Q[0]*" + invQ.x +
                                    " - _IMU_Q[3]*" + invQ.y + " + _IMU_Q[2]*" + invQ.z + ";");
                writer.WriteLine("  _y = _IMU_Q[2]*" + invQ.w + " + _IMU_Q[3]*" + invQ.x +
                                    " + _IMU_Q[0]*" + invQ.y + " - _IMU_Q[1]*" + invQ.z + ";");
                writer.WriteLine("  _z = _IMU_Q[3]*" + invQ.w + " - _IMU_Q[2]*" + invQ.x +
                                    " + _IMU_Q[1]*" + invQ.y + " + _IMU_Q[0]*" + invQ.z + ";");
                writer.WriteLine("  _roll = atan2(2*(_w*_x + _y*_z), 1 - 2*(_x*_x + _y*_y));");
                writer.WriteLine("  _pitch = asin(2*(_w*_y - _z*_x));");
                if (IMU_compensatory)
                {
                    for (int i = 0; i < channels.Count; i++)
                    {
                        if (Motion.p_gain[channels[i]] != 0 && Motion.fbox2[channels[i]].SelectedIndex != 2)
                        {
                            if (Motion.fbox2[channels[i]].SelectedIndex == 0) // roll
                                writer.WriteLine("  mixOffsets.mixoffsets[" + i + "] = " +
                                                 "(long)((180*_roll*" + Motion.p_gain[channels[i]] + ")/M_PI);");
                            else if (Motion.fbox2[channels[i]].SelectedIndex == 1) // pitch
                                writer.WriteLine("  mixOffsets.mixoffsets[" + i + "] = " +
                                                 "(long)((180*_pitch*" + Motion.p_gain[channels[i]] + ")/M_PI);");
                        }
                    }
                    writer.WriteLine("  mixOffsets.setMixOffsets();\n");
                }

                for (int i = 0; i < ME_Motionlist.Count; i++)
                {
                    ME_Motion m = (ME_Motion)ME_Motionlist[i];
                    if (m.trigger_method == (int)mtest_method.acc)
                    {
                        string stmts = "";
                        if (m.acc_Settings[0] < m.acc_Settings[1])
                            stmts += "(_IMU_val[0] >= " + m.acc_Settings[0] + " && _IMU_val[0] <= " + m.acc_Settings[1] + ") &&\n";
                        else
                            stmts += "(_IMU_val[0] >= " + m.acc_Settings[1] + " && _IMU_val[0] <= " + m.acc_Settings[0] + ") &&\n";
                        if (m.acc_Settings[2] < m.acc_Settings[3])
                            stmts += "         (_IMU_val[1] >= " + m.acc_Settings[2] + " && _IMU_val[1] <= " + m.acc_Settings[3] + ") &&\n";
                        else
                            stmts += "         (_IMU_val[1] >= " + m.acc_Settings[3] + " && _IMU_val[1] <= " + m.acc_Settings[2] + ") &&\n";
                        if (m.acc_Settings[4] < m.acc_Settings[5])
                            stmts += "         (_IMU_val[2] >= " + m.acc_Settings[4] + " && _IMU_val[2] <= " + m.acc_Settings[5] + ")";
                        else
                            stmts += "         (_IMU_val[2] >= " + m.acc_Settings[5] + " && _IMU_val[2] <= " + m.acc_Settings[4] + ")";

                        writer.WriteLine("  switch(" + m.name + "::acc_state)\n  {\n" +
                                         "    case 0:\n      if(" + stmts + ")\n      {\n" +
                                         "        " + m.name + "::acc_state = 1;\n" +
                                         "        " + m.name + "::acc_time = millis();\n      }\n      break;\n" +
                                         "    case 1:\n      if(" + stmts + ")\n" + "      {\n" +
                                         "        if(millis() - " + m.name + "::acc_time >= " + m.acc_Settings[6] + ")\n" +
                                         "          " + m.name + "::acc_state = 2;\n      }\n      else\n" +
                                         "        " + m.name + "::acc_state = 0;\n      break;\n    default:\n" +
                                         "      break;\n  }");
                    }
                }
                writer.WriteLine("  _IMU_update_time = millis();");
                writer.WriteLine("}");
            }
        }

        private void generate_isBlocked(TextWriter writer)
        {
            string l0 = "", l1 = "";
            writer.WriteLine("bool isBlocked(int layer)\n{");
            for (int i = 0; i < ME_Motionlist.Count; i++)
            {
                ME_Motion m = (ME_Motion)ME_Motionlist[i];
                if (m.moton_layer == 0 && m.property == (int)motion_property.blocking)
                    l0 += "    if(external_trigger[_" + m.name.ToUpper() + "]) return true;\n";
                else if (m.moton_layer == 1 && m.property == (int)motion_property.blocking)
                    l1 += "    if(external_trigger[_" + m.name.ToUpper() + "]) return true;\n";
            }
            if (l0 != "")
                writer.WriteLine("  if(layer == 0)\n  {\n" + l0 + "  }");
            if (l1 != "" && l0 != "")
                writer.WriteLine("  else if(layer == 1)\n  {\n" + l1 + "  }");
            else if (l1 != "" && l0 == "")
                writer.WriteLine("  if(layer == 1)\n  {\n" + l1 + "  }");
            writer.WriteLine("  return false;");
            writer.WriteLine("}");
        }

        private void generate_closeTriggers(TextWriter writer)
        {
            string l0 = "", l1 = "";
            writer.WriteLine("void closeTriggers(int layer)\n{");
            for (int i = 0; i < ME_Motionlist.Count; i++)
            {
                ME_Motion m = (ME_Motion)ME_Motionlist[i];
                if (m.moton_layer == 0)
                    l0 += "    external_trigger[_" + m.name.ToUpper() + "]= false; " +
                          "internal_trigger[_" + m.name.ToUpper() + "]= false;\n";
                else if (m.moton_layer == 1)
                    l1 += "    external_trigger[_" + m.name.ToUpper() + "]= false; " +
                          "internal_trigger[_" + m.name.ToUpper() + "]= false;\n";
            }
            if (l0 != "")
                writer.WriteLine("  if(layer == 0)\n  {\n" + l0 + "  }");
            if (l1 != "" && l0 != "")
                writer.WriteLine("  else if(layer == 1)\n  {\n" + l1 + "  }");
            else if (l1 != "" && l0 == "")
                writer.WriteLine("  if(layer == 1)\n  {\n" + l1 + "  }");
            writer.WriteLine("}");
        }

        private void generate_updateTrigger(TextWriter writer)
        {
            writer.WriteLine("void updateTrigger()\n" + "{");
            int space_num = 2;
            string space = set_space(space_num);
            // get input
            if (method_flag[1])
            {
                writer.WriteLine("  usb.Task();");
            }
            if (method_flag[2])
            {
                writer.WriteLine("  if(" + bt_port + ".available()){ " + bt_port + "_Command = " + bt_port + ".read(); }");
                writer.WriteLine("  else { if(renew_bt) " + bt_port + "_Command = 0xFFF; }");
            }
            if (method_flag[3])
            {
                writer.WriteLine("  ps2x.read_gamepad();");
            }
            writer.WriteLine(space + "if(isBlocked(0)) goto L1;");
            for (int layer = 0; layer < 2; layer++)
            {
                writer.WriteLine("L" + layer + ":");
                if (layer == 1)
                    writer.WriteLine(space + "if(isBlocked(1)) return;");
                // update tirggers
                bool first = true;
                for (int i = 0; i < ME_Motionlist.Count; i++) //startup
                {
                    ME_Motion m = (ME_Motion)ME_Motionlist[i];
                    if (m.trigger_method == (int)mtest_method.always &&
                        m.auto_method == (int)auto_method.title && m.moton_layer == layer)
                    {
                        string update_mask = "";
                        if (layer == 1)
                            update_mask = "memcpy(servo_mask, " + m.name + "::mask, sizeof(servo_mask));";
                        if (first)
                        {
                            writer.WriteLine(space + "if(" + trigger_condition(m) + ") " +
                                             "{_curr_motion[" + layer + "] = _" + m.name.ToUpper() + "; " +
                                             m.name + "_title--;" + update_mask + "}");
                            first = false;
                        }
                        else
                            writer.WriteLine(space + "else if(" + trigger_condition(m) + ") " +
                                             "{_curr_motion[" + layer + "] = _" + m.name.ToUpper() + "; " +
                                             m.name + "_title--;" + update_mask + "}");
                    }
                }
                for (int i = 0; i < ME_Motionlist.Count; i++) //acc
                {
                    ME_Motion m = (ME_Motion)ME_Motionlist[i];
                    if (m.trigger_method == (int)mtest_method.acc && m.moton_layer == layer)
                    {
                        string update_mask = "";
                        if (layer == 1)
                            update_mask = "memcpy(servo_mask, " + m.name + "::mask, sizeof(servo_mask));";

                        if (first)
                        {
                            writer.WriteLine(space + "if(" + trigger_condition(m) + ") " +
                                                "{_curr_motion[" + layer + "] = _" + m.name.ToUpper() + ";" +
                                                m.name + "::acc_state = 0;" + update_mask + "}");
                            first = false;
                        }
                        else
                            writer.WriteLine(space + "else if(" + trigger_condition(m) + ") " +
                                                "{_curr_motion[" + layer + "] = _" + m.name.ToUpper() + ";" +
                                                m.name + "::acc_state = 0;" + update_mask + "}");
                    }
                }
                for (int i = 0; i < ME_Motionlist.Count; i++) //bt
                {
                    ME_Motion m = (ME_Motion)ME_Motionlist[i];
                    if (m.trigger_method == (int)mtest_method.bluetooth && m.moton_layer == layer)
                    {
                        string renew_bt = "";
                        string update_mask = "";
                        if (layer == 1)
                            update_mask = "memcpy(servo_mask, " + m.name + "::mask, sizeof(servo_mask));";

                        if (String.Compare(m.bt_mode, "OneShot") == 0)
                            renew_bt = "renew_bt = true;";
                        else
                            renew_bt = "renew_bt = false;";
                        if (first)
                        {
                            writer.WriteLine(space + "if(" + trigger_condition(m) + ") " +
                                                "{_curr_motion[" + layer + "] = _" + m.name.ToUpper() + ";" +
                                                renew_bt + update_mask + "}");
                            first = false;
                        }
                        else
                            writer.WriteLine(space + "else if(" + trigger_condition(m) + ") " +
                                                "{_curr_motion[" + layer + "] = _" + m.name.ToUpper() + ";" +
                                                renew_bt + update_mask + "}");
                    }
                }
                for (int i = 0; i < ME_Motionlist.Count; i++)
                {
                    ME_Motion m = (ME_Motion)ME_Motionlist[i];
                    if (!(m.trigger_method == (int)mtest_method.always && m.auto_method == (int)auto_method.on) &&
                        !(m.trigger_method == (int)mtest_method.always && m.auto_method == (int)auto_method.title) &&
                        !(m.trigger_method == (int)mtest_method.bluetooth) && !(m.trigger_method == (int)mtest_method.acc) &&
                        m.moton_layer == layer)
                    {
                        string cancel_release = "";
                        string update_mask = "";
                        if (layer == 1)
                            update_mask = "memcpy(servo_mask, " + m.name + "::mask, sizeof(servo_mask));";
                        
                        if (m.trigger_method == (int)mtest_method.keyboard && m.trigger_keyType == (int)keyboard_method.release)
                            cancel_release = "keys_state[" + convert_keynum(m.trigger_key) + "] = 0;";
                        if (first)
                        {
                            writer.WriteLine(space + "if(" + trigger_condition(m) + ") " +
                                             "{_curr_motion[" + layer + "] = _" + m.name.ToUpper() + ";" +
                                             cancel_release + update_mask + "}");
                            first = false;
                        }
                        else
                            writer.WriteLine(space + "else if(" + trigger_condition(m) + ") " +
                                             "{_curr_motion[" + layer + "] = _" + m.name.ToUpper() + ";" +
                                             cancel_release + update_mask + "}");
                    }
                }
                for (int i = 0; i < ME_Motionlist.Count; i++) //always
                {
                    ME_Motion m = (ME_Motion)ME_Motionlist[i];
                    if (m.trigger_method == (int)mtest_method.always &&
                        m.auto_method == (int)auto_method.on && m.moton_layer == layer)
                    {
                        string update_mask = "";
                        if (layer == 1)
                            update_mask = "memcpy(servo_mask, " + m.name + "::mask, sizeof(servo_mask));";
                        if (first)
                        {
                            writer.WriteLine(space + "if(" + trigger_condition(m) + ") " +
                                             "{_curr_motion[" + layer + "] = _" + m.name.ToUpper() + ";" + update_mask + "}");
                            first = false;
                        }
                        else
                            writer.WriteLine(space + "else if(" + trigger_condition(m) + ") " +
                                             "{_curr_motion[" + layer + "] = _" + m.name.ToUpper() + ";" + update_mask + "}");
                    }
                }
                if (first)
                    writer.WriteLine(space + "_curr_motion[" + layer + "] = _NONE;");
                else
                {
                    if (layer == 0)
                        writer.WriteLine(space + "else _curr_motion[" + layer + "] = _NONE;");
                    else
                        writer.WriteLine(space + "else {_curr_motion[" + layer + "] = _NONE;" +
                                         "memset(servo_mask, 0, sizeof(servo_mask));}");
                }
                writer.WriteLine(space + "if(_last_motion[" + layer + "] != _curr_motion[" + layer +
                                 "] && _curr_motion[" + layer + "] != _NONE)");
                writer.WriteLine(space + "{\n    closeTriggers(" + layer + ");\n    external_trigger[_curr_motion[" +
                                 layer + "]] = true;");
                for (int i = 0; i < ME_Motionlist.Count; i++)
                {
                    ME_Motion m = (ME_Motion)ME_Motionlist[i];
                    if (m.moton_layer == layer)
                    {
                        writer.WriteLine(space + "  " + m.name + "::state = 0;");
                        for (int j = 0; j < m.goto_var.Count; j++)
                            writer.WriteLine(space + "  " + m.name + "::" + m.goto_var[j] + " = 0;");
                    }
                }
                writer.WriteLine(space + "}");
                writer.WriteLine(space + "external_trigger[_curr_motion[" + layer + "]] = true;");
                writer.WriteLine(space + "_last_motion[" + layer + "] = _curr_motion[" + layer + "];");
            }
            space_num -= 2;
            space = set_space(space_num);
            writer.WriteLine("}");
        }

        private void generate_forvar(ME_Motion m)
        {
            for (int i = 0, flag_count = 0; i < m.Events.Count; i++)
            {
                if (m.Events[i] is ME_Flag)
                {
                    for (int k = 0; k < m.Events.Count; k++)
                    {
                        if (m.Events[k] is ME_Goto)
                        {
                            if (String.Compare(((ME_Flag)m.Events[i]).name, ((ME_Goto)m.Events[k]).name) == 0)
                            {
                                ME_Goto g = (ME_Goto)m.Events[k];
                                if (g.is_goto)
                                {
                                    string for_var = m.name + "_" + g.name + "_" + flag_count.ToString();
                                    ((ME_Flag)m.Events[i]).var = for_var;
                                    flag_count++;
                                    break;
                                }
                            }
                        }
                        else if (m.Events[k] is ME_If)
                        {
                            if (String.Compare(((ME_Flag)m.Events[i]).name, ((ME_If)m.Events[k]).name) == 0)
                            {
                                ME_If mif = (ME_If)m.Events[k];
                                string for_var = m.name + "_" + mif.name + "_" + flag_count.ToString();
                                ((ME_Flag)m.Events[i]).var = for_var;
                                flag_count++;
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void generate_motion(ME_Motion m, string frm_name, TextWriter writer, int channels)
        {
            string space = set_space(2);
            string space4 = set_space(4);
            writer.WriteLine("void " + m.name + "Update()\n{");
            writer.WriteLine(space + "switch(" + m.name + "::state)\n  {");
            writer.WriteLine(space + "case " + m.name + "::IDLE:");
            if (m.states.Count > 1)
                writer.WriteLine(space4 + "if(external_trigger[_" + m.name.ToUpper() + "] || internal_trigger[_" +
                                 m.name.ToUpper() + "]) " + m.name + "::state = " + m.name + "::" + m.states[1] + ";");
            else
                writer.WriteLine(space4 + "if(external_trigger[_" + m.name.ToUpper() + "] || internal_trigger[_" +
                                 m.name.ToUpper() + "]) " + m.name + "::state = " + m.name + "::" + m.states[0] + ";");
            writer.WriteLine(space4 + "else break;");
            string next_action = "";
            int state_counter = 1;
            generate_forvar(m);
            for (int i = 0; i < m.Events.Count; i++)
            {
                if (m.Events[i] is ME_Frame)
                {
                    ME_Frame f = (ME_Frame)m.Events[i];
                    string mask_str = "(~servo_mask[i])";
                    if (m.moton_layer == 1)
                        mask_str = "servo_mask[i]";
                    writer.WriteLine(space + "case " + m.name + "::FRAME_" + i + ":");
                    writer.WriteLine(space4 + "for(int i = " + channels + "; i-- > 0; )");
                    writer.WriteLine(space4 + "  _86ME_RUN.positions[i] = " + frm_name +
                                     "[" + f.num + "].positions[i] & " + mask_str + ";");
                    writer.WriteLine(space4 + "_86ME_RUN.playPositions((unsigned long)" + f.delay + ");");
                    writer.WriteLine(space4 + m.name + "::time = millis();");
                    writer.WriteLine(space4 + m.name + "::state = "+ m.name + "::WAIT_FRAME_" + i + ";");
                    writer.WriteLine(space + "case " + m.name + "::WAIT_FRAME_" + i + ":");
                    writer.WriteLine(space4 + "if(millis() - " + m.name + "::time >= " + f.delay + ")");
                    if (i != m.Events.Count - 1)
                    {
                        state_counter += 2;
                        next_action = m.name + "::" + m.states[state_counter];
                        writer.WriteLine(space4 + "  " + m.name + "::state = " + next_action + ";");
                    }
                    else
                    {
                        next_action = m.name + "::IDLE";
                        writer.WriteLine(space4 + "{\n      " + m.name + "::state = " + next_action + ";");
                        for (int j = 0; j < m.goto_var.Count; j++)
                            writer.WriteLine(space4 + "  " + m.name + "::" + m.goto_var[j] + " = 0;");
                        writer.WriteLine(space4 + "  internal_trigger[_" + m.name.ToUpper() + "] = false;");
                        writer.WriteLine(space4 + "  external_trigger[_" + m.name.ToUpper() + "] = false;");
                        writer.WriteLine(space4 + "}");
                    }
                    writer.WriteLine(space4 + "break;");
                }
                else if (m.Events[i] is ME_Delay)
                {
                    ME_Delay d = (ME_Delay)m.Events[i];
                    writer.WriteLine(space + "case " + m.name + "::DELAY_" + i + ":");
                    writer.WriteLine(space4 + m.name + "::time = millis();");
                    writer.WriteLine(space4 + m.name + "::state = " + m.name + "::WAIT_DELAY_" + i + ";");
                    writer.WriteLine(space + "case " + m.name + "::WAIT_DELAY_" + i + ":");
                    writer.WriteLine(space4 + "if(millis() - " + m.name + "::time >= " + d.delay + ")");
                    if (i != m.Events.Count - 1)
                    {
                        state_counter += 2;
                        next_action = m.name + "::" + m.states[state_counter];
                        writer.WriteLine(space4 + "  " + m.name + "::state = " + next_action + ";");
                    }
                    else
                    {
                        next_action = m.name + "::IDLE";
                        writer.WriteLine(space4 + "{\n      " + m.name + "::state = " + next_action + ";");
                        for (int j = 0; j < m.goto_var.Count; j++)
                            writer.WriteLine(space4 + "  " + m.name + "::" + m.goto_var[j] + " = 0;");
                        writer.WriteLine(space4 + "  internal_trigger[_" + m.name.ToUpper() + "] = false;");
                        writer.WriteLine(space4 + "  external_trigger[_" + m.name.ToUpper() + "] = false;");
                        writer.WriteLine(space4 + "}");
                    }
                    writer.WriteLine(space4 + "break;");
                }
                else if (m.Events[i] is ME_Flag)
                {
                    writer.WriteLine(space + "case " + m.name + "::FLAG_" + i + ":");
                    state_counter++;
                    for (int k = 0; k < m.Events.Count; k++)
                    {
                        if (m.Events[k] is ME_Goto)
                        {
                            if (String.Compare(((ME_Flag)m.Events[i]).name, ((ME_Goto)m.Events[k]).name) == 0)
                            {
                                ME_Goto g = (ME_Goto)m.Events[k];
                                if (g.is_goto)
                                {
                                    writer.WriteLine(space4 + "flag_" + ((ME_Flag)m.Events[i]).var + ":");
                                    break;
                                }
                            }
                        }
                        else if (m.Events[k] is ME_If)
                        {
                            if (String.Compare(((ME_Flag)m.Events[i]).name, ((ME_If)m.Events[k]).name) == 0)
                            {
                                ME_If mif = (ME_If)m.Events[k];
                                writer.WriteLine(space4 + "flag_" + ((ME_Flag)m.Events[i]).var + ":");
                                break;
                            }
                        }
                    }
                    if (i == m.Events.Count - 1)
                    {
                        writer.WriteLine(space4 + m.name + "::state = " + m.name + "::IDLE;");
                        writer.WriteLine(space4 + "internal_trigger[_" + m.name.ToUpper() + "] = false;");
                        writer.WriteLine(space4 + "external_trigger[_" + m.name.ToUpper() + "] = false;");
                        for (int j = 0; j < m.goto_var.Count; j++)
                            writer.WriteLine(space4 + m.name + "::" + m.goto_var[j] + " = 0;");
                        writer.WriteLine(space4 + "break;");
                    }
                }
                else if (m.Events[i] is ME_Goto)
                {
                    ME_Goto g = (ME_Goto)m.Events[i];
                    writer.WriteLine(space + "case " + m.name + "::GOTO_" + i + ":");
                    state_counter++;
                    if (g.is_goto)
                    {
                        for (int k = 0; k < m.Events.Count; k++)
                        {
                            if (m.Events[k] is ME_Flag)
                            {
                                if (String.Compare(g.name, ((ME_Flag)m.Events[k]).name) == 0)
                                {
                                    string for_var = ((ME_Flag)m.Events[k]).var;
                                    if (((ME_Goto)m.Events[i]).infinite == false)
                                    {
                                        writer.WriteLine(space4 + "if(" + m.name + "::" + g.name + "_" + i +
                                                         "++ < " + g.loops + ") goto flag_" + for_var + ";");
                                        break;
                                    }
                                    else
                                    {
                                        writer.WriteLine(space4 + "if(1) goto flag_" + for_var + ";");
                                        break;
                                    }
                                }
                            }
                        }
                        if (i != m.Events.Count - 1)
                        {
                            next_action = m.name + "::" + m.states[state_counter];
                            if (g.infinite == false)
                                writer.WriteLine(space4 + m.name + "::" + g.name + "_" + i + " = 0;");
                            writer.WriteLine(space4 + m.name + "::state = " + next_action + ";");
                        }
                        else
                        {
                            writer.WriteLine(space4 + "else\n    {");
                            next_action = m.name + "::IDLE";
                            for (int j = 0; j < m.goto_var.Count; j++)
                                writer.WriteLine(space4 + "  " + m.name + "::" + m.goto_var[j] + " = 0;");
                            writer.WriteLine(space4 + "  internal_trigger[_" + m.name.ToUpper() + "] = false;");
                            writer.WriteLine(space4 + "  external_trigger[_" + m.name.ToUpper() + "] = false;");
                            writer.WriteLine(space4 + "  " + m.name + "::state = " + next_action + ";");
                            writer.WriteLine(space4 + "}");
                        }
                        writer.WriteLine(space4 + "break;");
                    }
                }
                else if (m.Events[i] is ME_Trigger)
                {
                    ME_Trigger t = (ME_Trigger)m.Events[i];
                    writer.WriteLine(space + "case " + m.name + "::MOTION_" + i + ":");
                    if (m.moton_layer == 1)
                    {
                        if (i != m.Events.Count - 1)
                        {
                            state_counter += 2;
                            next_action = m.name + "::" + m.states[state_counter];
                            writer.WriteLine(space4 + m.name + "::state = " + next_action + ";");
                        }
                        else
                        {
                            next_action = m.name + "::IDLE";
                            writer.WriteLine(space4 + m.name + "::state = " + next_action + ";");
                            for (int j = 0; j < m.goto_var.Count; j++)
                                writer.WriteLine(space4 + m.name + "::" + m.goto_var[j] + " = 0;");
                            writer.WriteLine(space4 + "internal_trigger[_" + m.name.ToUpper() + "] = false;");
                            writer.WriteLine(space4 + "external_trigger[_" + m.name.ToUpper() + "] = false;");
                            writer.WriteLine(space4 + "break;");
                        }
                        continue;
                    }
                    if (String.Compare(t.name, "") != 0)
                    {
                        for (int j = 0; j < ME_Motionlist.Count; j++) //reset goto_var of the triggered motion
                        {
                            ME_Motion tr_m = (ME_Motion)ME_Motionlist[j];
                            if (tr_m.name == t.name)
                            {
                                for (int k = 0; k < tr_m.goto_var.Count; k++)
                                    writer.WriteLine(space4 + tr_m.name + "::" + tr_m.goto_var[k] + " = 0;");
                            }
                        }
                    }
                    if (String.Compare(t.name, "") == 0)
                    {
                        if (i != m.Events.Count - 1)
                        {
                            state_counter += 2;
                            next_action = m.name + "::" + m.states[state_counter];
                            writer.WriteLine(space4 + m.name + "::state = " + next_action + ";");
                        }
                        else
                        {
                            next_action = m.name + "::IDLE";
                            writer.WriteLine(space4 + m.name + "::state = " + next_action + ";");
                            for (int j = 0; j < m.goto_var.Count; j++)
                                writer.WriteLine(space4 + m.name + "::" + m.goto_var[j] + " = 0;");
                            writer.WriteLine(space4 + "internal_trigger[_" + m.name.ToUpper() + "] = false;");
                            writer.WriteLine(space4 + "external_trigger[_" + m.name.ToUpper() + "] = false;");
                        }
                    }
                    else if (t.method == (int)internal_trigger.call)
                    {
                        writer.WriteLine(space4 + m.name + "::state = " + m.name + "::WAIT_MOTION_" + i + ";");
                        writer.WriteLine(space4 + "internal_trigger[_" + t.name.ToUpper() + "] = true;");
                        writer.WriteLine(space4 + t.name + "::state = " + t.name + "::IDLE;");
                        writer.WriteLine(space + "case " + m.name + "::WAIT_MOTION_" + i + ":");
                        writer.WriteLine(space4 + "if(!internal_trigger[_" + t.name.ToUpper() + "])");
                        if (i != m.Events.Count - 1)
                        {
                            state_counter += 2;
                            next_action = m.name + "::" + m.states[state_counter];
                            writer.WriteLine(space4 + "  " + m.name + "::state = " + next_action + ";");
                        }
                        else
                        {
                            next_action = m.name + "::IDLE";
                            writer.WriteLine(space4 + "{\n      " + m.name + "::state = " + next_action + ";");
                            for (int j = 0; j < m.goto_var.Count; j++)
                                writer.WriteLine(space4 + "  " + m.name + "::" + m.goto_var[j] + " = 0;");
                            writer.WriteLine(space4 + "  internal_trigger[_" + m.name.ToUpper() + "] = false;");
                            writer.WriteLine(space4 + "  external_trigger[_" + m.name.ToUpper() + "] = false;");
                            writer.WriteLine(space4 + "}");
                        }
                    }
                    else if (t.method == (int)internal_trigger.jump)
                    {
                        writer.WriteLine(space4 + m.name + "::state = " + m.name + "::IDLE;");
                        writer.WriteLine(space4 + "internal_trigger[_" + m.name.ToUpper() + "] = false;");
                        writer.WriteLine(space4 + "external_trigger[_" + m.name.ToUpper() + "] = false;");
                        writer.WriteLine(space4 + "internal_trigger[_" + t.name.ToUpper() + "] = true;");
                        writer.WriteLine(space4 + t.name + "::state = " + t.name + "::IDLE;");
                        for (int j = 0; j < m.goto_var.Count; j++)
                            writer.WriteLine(space4 + m.name + "::" + m.goto_var[j] + " = 0;");
                    }
                    writer.WriteLine(space4 + "break;");
                }
                else if (m.Events[i] is ME_Release)
                {
                    writer.WriteLine(space + "case " + m.name + "::RELEASE_" + i + ":");
                    writer.WriteLine(space4 + "for(int i = " + channels + "; i-- > 0; )");
                    writer.WriteLine(space4 + "  used_servos[i].release();");
                    state_counter++;
                    if (i != m.Events.Count - 1)
                    {
                        next_action = m.name + "::" + m.states[state_counter];
                        writer.WriteLine(space4 + m.name + "::state = " + next_action + ";");
                    }
                    else
                    {
                        next_action = m.name + "::IDLE";
                        writer.WriteLine(space4 + m.name + "::state = " + next_action + ";");
                        for (int j = 0; j < m.goto_var.Count; j++)
                            writer.WriteLine(space4 + m.name + "::" + m.goto_var[j] + " = 0;");
                        writer.WriteLine(space4 + "internal_trigger[_" + m.name.ToUpper() + "] = false;");
                        writer.WriteLine(space4 + "external_trigger[_" + m.name.ToUpper() + "] = false;");
                    }
                    writer.WriteLine(space4 + "break;");
                }
                else if (m.Events[i] is ME_If)
                {
                    ME_If mif = (ME_If)m.Events[i];
                    writer.WriteLine(space + "case " + m.name + "::IF_" + i + ":");
                    writer.WriteLine(space4 + "if(" + var2str(mif.left_var) + method2str(mif.method) + var2str(mif.right_var) + ")");
                    bool hasTarget = false;
                    for (int k = 0; k < m.Events.Count; k++)
                    {
                        if (m.Events[k] is ME_Flag)
                        {
                            if (String.Compare(mif.name, ((ME_Flag)m.Events[k]).name) == 0)
                            {
                                writer.WriteLine(space4 + "  goto flag_" + ((ME_Flag)m.Events[k]).var + ";");
                                hasTarget = true;
                                break;
                            }
                        }
                    }
                    if (!hasTarget)
                        writer.WriteLine(space4 + "  ;");
                    state_counter++;
                    if (i != m.Events.Count - 1)
                    {
                        next_action = m.name + "::" + m.states[state_counter];
                        writer.WriteLine(space4 + m.name + "::state = " + next_action + ";");
                    }
                    else
                    {
                        next_action = m.name + "::IDLE";
                        writer.WriteLine(space4 + m.name + "::state = " + next_action + ";");
                        for (int j = 0; j < m.goto_var.Count; j++)
                            writer.WriteLine(space4 + m.name + "::" + m.goto_var[j] + " = 0;");
                        writer.WriteLine(space4 + "internal_trigger[_" + m.name.ToUpper() + "] = false;");
                        writer.WriteLine(space4 + "external_trigger[_" + m.name.ToUpper() + "] = false;");
                    }
                    writer.WriteLine(space4 + "break;");
                }
                else if (m.Events[i] is ME_Compute)
                {
                    ME_Compute op = (ME_Compute)m.Events[i];
                    writer.WriteLine(space + "case " + m.name + "::COMPUTE_" + i + ":");
                    switch(op.form)
                    {
                        case 0:
                            writer.WriteLine(op2str(op.left_var, op.f1_var1, op.f1_var2, op.f1_op, 0));
                            break;
                        case 1:
                            writer.WriteLine(op2str(op.left_var, 0, op.f2_var, op.f2_op, 1));
                            break;
                        case 2:
                            writer.WriteLine(space4 + var2str(op.left_var) + " = " + var2str(op.f3_var) + ";");
                            break;
                        case 3:
                            writer.WriteLine(space4 + var2str(op.left_var) + " = " + op.f4_const + ";");
                            break;
                        default:
                            break;
                    }
                    state_counter++;
                    if (i != m.Events.Count - 1)
                    {
                        next_action = m.name + "::" + m.states[state_counter];
                        writer.WriteLine(space4 + m.name + "::state = " + next_action + ";");
                    }
                    else
                    {
                        next_action = m.name + "::IDLE";
                        writer.WriteLine(space4 + m.name + "::state = " + next_action + ";");
                        for (int j = 0; j < m.goto_var.Count; j++)
                            writer.WriteLine(space4 + m.name + "::" + m.goto_var[j] + " = 0;");
                        writer.WriteLine(space4 + "internal_trigger[_" + m.name.ToUpper() + "] = false;");
                        writer.WriteLine(space4 + "external_trigger[_" + m.name.ToUpper() + "] = false;");
                        writer.WriteLine(space4 + "break;");
                    }
                }
            }
            writer.WriteLine(space + "default:");
            writer.WriteLine(space4 + "break;");
            writer.WriteLine("  }"); //switch
            writer.WriteLine("}");
        }

        public void generate_declaration(TextWriter writer, List<int> channels, List<uint> home, bool isAllinOne)
        {
            string frm_name;
            writer.WriteLine("#include <time.h>");
            writer.WriteLine("#include <math.h>");
            writer.WriteLine("#include <Servo86.h>");
            if (method_flag[1]) // keyboard
                writer.WriteLine("#include <KeyboardController.h>");
            if (method_flag[3]) // ps2
                writer.WriteLine("#include <PS2X_lib.h>");
            if (method_flag[4]) // acc
                writer.WriteLine("#include <EEPROM.h>\n#include \"FreeIMU1.h\"\n#include <Wire.h>");
            writer.WriteLine();

            writer.WriteLine("double _86ME_var[50] = {0};");
            writer.WriteLine("double _roll = 0;");
            writer.WriteLine("double _pitch = 0;");
            writer.WriteLine("double _IMU_val[9] = {0};");
            writer.WriteLine("double _IMU_Q[4] = {1, 0, 0, 0};");
            writer.WriteLine("int _IMU_init_status;");
            if (channels.Count > 0)
            {
                writer.WriteLine("int servo_mask[" + channels.Count + "] = {0};");
                writer.WriteLine("Servo used_servos[" + channels.Count + "];");
            }
            else
            {
                writer.WriteLine("int servo_mask[1] = {0};");
                writer.WriteLine("Servo used_servos[1];");
            }
            writer.WriteLine();
            writer.Write("enum {");
            for (int i = 0; i < ME_Motionlist.Count; i++)
                writer.Write("_" + ((ME_Motion)ME_Motionlist[i]).name.ToUpper() + ", ");
            writer.Write("_NONE");
            writer.WriteLine("};");
            writer.WriteLine("int _last_motion[2] = {_NONE, _NONE};");
            writer.WriteLine("int _curr_motion[2] = {_NONE, _NONE};");
            int triggers_num = ME_Motionlist.Count + 1;
            writer.WriteLine("bool internal_trigger[" + triggers_num + "] = {0};");
            writer.WriteLine("bool external_trigger[" + triggers_num + "] = {0};");
            writer.WriteLine();
            if (isAllinOne)
                writer.WriteLine("ServoOffset offsets;");
            else
                writer.WriteLine("ServoOffset offsets(\"" + "_86ME_settings\\\\" + "86offset.txt\");");
            writer.WriteLine();
            writer.WriteLine("ServoFrame _86ME_HOME;");// automatic homeframe
            writer.WriteLine("ServoFrame _86ME_RUN;\n");

            for (int i = 0; i < ME_Motionlist.Count; i++)
            {
                frm_name = ((ME_Motion)ME_Motionlist[i]).name.ToString() + "_frm";
                if (((ME_Motion)ME_Motionlist[i]).trigger_method == (int)mtest_method.always &&
                    ((ME_Motion)ME_Motionlist[i]).auto_method == (int)auto_method.title)
                    writer.WriteLine("int " + ((ME_Motion)ME_Motionlist[i]).name + "_title = 1;");
                writer.WriteLine("ServoFrame " + frm_name + "[" + ((ME_Motion)ME_Motionlist[i]).frames + "];");
            }
            writer.WriteLine();

            if (method_flag[1]) // keyboard
            {
                writer.WriteLine("USBHost usb;");
                writer.WriteLine("KeyboardController keyboard(usb);");
                writer.WriteLine("static int keys_state[128] = {0};");
                writer.WriteLine("static int keys_pressed[128] = {0};");
                writer.WriteLine("void keyPressed(){keys_pressed[keyboard.getOemKey()] = 1;}");
                writer.WriteLine("void keyReleased(){keys_pressed[keyboard.getOemKey()] = 0;}");
                writer.WriteLine("int key_state(int k)\n{\n  if(keys_pressed[k] == 1 && (keys_state[k]&2) != 2)\n" +
		                         "  {\n    keys_state[k] = 2;\n    return 2;\n  }\n" +
                                 "  else if(keys_pressed[k] == 1 && (keys_state[k]&2) == 2)\n  {\n" +
                                 "    keys_state[k] = 3;\n    return 3;\n  }\n" +
                                 "  else if(keys_pressed[k] == 0 && (keys_state[k]&2) == 2)\n  {\n" +
                                 "    keys_state[k] = 1;\n    return 1;\n  }\n" +
                                 "  else\n  {\n    keys_state[k] = 0;\n    return 0;\n  }\n}");
            }
            if (method_flag[2]) // bt
            {
                writer.WriteLine("int " + bt_port + "_Command = 0xFFF;");
                writer.WriteLine("bool renew_bt = true;");
            }
            if (method_flag[3]) // ps2
                writer.WriteLine("PS2X ps2x;");
            if (method_flag[4]) // acc
            {
                writer.WriteLine("unsigned long _IMU_update_time = millis();");
                writer.WriteLine("FreeIMU1 _IMU = FreeIMU1();");
            }
            if (IMU_compensatory)
            {
                writer.WriteLine("ServoMixOffset mixOffsets;");
            }
            writer.WriteLine();

            for (int i = 0; i < ME_Motionlist.Count; i++)
                generate_variable((ME_Motion)ME_Motionlist[i], writer, channels);
            generate_updateIMU(writer, channels);
            generate_isBlocked(writer);
            generate_closeTriggers(writer);
            generate_updateTrigger(writer);
            for (int i = 0; i < ME_Motionlist.Count; i++)
            {
                ME_Motion m = (ME_Motion)ME_Motionlist[i];
                frm_name = ((ME_Motion)ME_Motionlist[i]).name.ToString() + "_frm";
                generate_motion(m, frm_name, writer, channels.Count);
            }
        }

        public void generate_setup(TextWriter writer, List<int> channels, List<uint> home, bool isAllinOne, List<int> angle = null)
        {
            string frm_name;
            int processed = 0;
            writer.WriteLine("void setup()");
            writer.WriteLine("{");
            writer.WriteLine("  srand(time(NULL));");
            if (method_flag[2]) // bt
                writer.WriteLine("  " + bt_port + ".begin(" + bt_baud + ");");
            writer.WriteLine();
            if (method_flag[3]) // ps2
                writer.WriteLine("  ps2x.config_gamepad(" + ps2_pins[3] + ", " + ps2_pins[1] +
                                 ", " + ps2_pins[2] + ", " + ps2_pins[0] + ", false, false);\n");
            if (method_flag[4]) // acc
            {
                if (Motion.comboBox2.SelectedIndex == 1) //LSM330DLC
                    writer.WriteLine("  Wire.begin();\n  delay(5);\n  _IMU_init_status = _IMU.initEX(0);\n  delay(5);\n");
                else if (Motion.comboBox2.SelectedIndex == 2) //RM-G146
                    writer.WriteLine("  Wire.begin();\n  delay(5);\n  _IMU_init_status = _IMU.initEX(2);\n  delay(5);\n");
                else // NONE
                    writer.WriteLine("  Wire.begin();\n  delay(5);\n  _IMU_init_status = _IMU.init();\n  delay(5);\n");
            }
            for (int i = 0; i < channels.Count; i++)
                writer.WriteLine("  used_servos[" + i.ToString() + "].attach(" + channels[i].ToString() + ");");
            writer.WriteLine();

            if (isAllinOne)
            {
                int offset_count = 0;
                for (int i = 0; i < 45; i++)
                {
                    if (offset[i] != 0 && String.Compare(Motion.fbox[i].Text, "---noServo---") != 0)
                        writer.WriteLine("  offsets.offsets[" + offset_count.ToString() + "] = " + offset[i].ToString() + ";");
                    if (String.Compare(Motion.fbox[i].Text, "---noServo---") != 0)
                        offset_count++;
                }
                writer.WriteLine();

                for (int k = 0; k < ME_Motionlist.Count; k++)
                {
                    frm_name = ((ME_Motion)ME_Motionlist[k]).name.ToString() + "_frm";
                    for (int i = 0; i < ((ME_Motion)ME_Motionlist[k]).frames; i++)
                    {
                        for (int j = 0; j < channels.Count; j++)
                            writer.WriteLine("  " + frm_name + "[" + i + "].positions[" + j.ToString() + "] = " +
                                             angle[processed + i * channels.Count + j] + ";");
                        writer.WriteLine();
                    }
                    processed += channels.Count * ((ME_Motion)ME_Motionlist[k]).frames;
                }
            }
            else
            {
                for (int i = 0; i < ME_Motionlist.Count; i++)
                {
                    int frame_count = ((ME_Motion)ME_Motionlist[i]).frames;
                    string current_motion_name = ((ME_Motion)ME_Motionlist[i]).name;
                    frm_name = ((ME_Motion)ME_Motionlist[i]).name + "_frm";
                    for (int j = 0; j < frame_count; j++)
                    {
                        writer.WriteLine("  " + frm_name + "[" + j + "].load(\"" + "_86ME_settings\\\\" +
                                         current_motion_name + "_frm" + j + ".txt\");");
                    }
                }
            }

            for (int j = 0; j < channels.Count; j++)
                writer.WriteLine("  _86ME_HOME.positions[" + j.ToString() + "] = " + home[j] + ";");
            writer.WriteLine();
            writer.WriteLine("  offsets.setOffsets();");
            writer.WriteLine();
            if (method_flag[4])
            {
                writer.WriteLine("  if(_IMU_init_status == 0)\n  {\n    for(int i = 0; i < 10; i++)\n" +
                                 "    {\n      _IMU.getQ(_IMU_Q, _IMU_val);\n" +
                                 "      delay(50);\n    }\n  }\n");
            }
            writer.WriteLine("  _86ME_HOME.playPositions((unsigned long)0);");

            writer.WriteLine("}");
            writer.WriteLine();
        }

        public void generate_loop(TextWriter writer)
        {
            writer.WriteLine("void loop()");
            writer.WriteLine("{");
            if (method_flag[4]) //acc
                writer.WriteLine("  updateIMU();");
            writer.WriteLine("  updateTrigger();");
            for (int j = 0; j < ME_Motionlist.Count; j++)
            {
                ME_Motion m = (ME_Motion)ME_Motionlist[j];
                writer.WriteLine("  " + m.name + "Update();");
            }
            writer.WriteLine("}");
        }

        public void generate_AllinOne()
        {
            
            FolderBrowserDialog path = new FolderBrowserDialog();
            var dialogResult = path.ShowDialog();
            string txtPath = path.SelectedPath;
            List<int> channels = new List<int>();
            List<int> angle = new List<int>();
            List<uint> home = new List<uint>();
            int count = 0;
            bool add_channel = true;

            if (dialogResult == DialogResult.OK && path.SelectedPath != null)
            {
                if (!Directory.Exists(path.SelectedPath))
                {
                    MessageBox.Show("The selected directory does not exist, please try again.");
                    return;
                }
                string motion_sketch_name = "\\AllinOne_Motion_Sketch";
                Directory.CreateDirectory(path.SelectedPath + motion_sketch_name);
                nfilename = path.SelectedPath + motion_sketch_name + motion_sketch_name + ".ino";
                TextWriter writer = new StreamWriter(nfilename);

                for (int i = 0; i < ME_Motionlist.Count; i++)
                {
                    count = 0;
                    for (int j = 0; j < ((ME_Motion)ME_Motionlist[i]).Events.Count; j++)
                    {
                        if (((ME_Motion)ME_Motionlist[i]).Events[j] is ME_Frame)
                        {
                            ME_Frame f = (ME_Frame)(((ME_Motion)ME_Motionlist[i]).Events[j]);
                            for (int k = 0; k < 45; k++)
                            {
                                if (String.Compare(Motion.fbox[k].Text, "---noServo---") != 0)
                                {
                                    if (f.type == 1)
                                        angle.Add(f.frame[k]);
                                    else if (f.type == 0)
                                        angle.Add(int.Parse(Motion.ftext2[k].Text));
                                    home.Add(uint.Parse(Motion.ftext2[k].Text));
                                    if (add_channel)
                                        channels.Add(k);
                                }
                            }
                            add_channel = false;
                            ((ME_Frame)((ME_Motion)ME_Motionlist[i]).Events[j]).num = count;
                            count++;
                        }
                    }
                    ((ME_Motion)ME_Motionlist[i]).frames = count;
                }

                generate_declaration(writer, channels, home, true);
                generate_setup(writer, channels, home, true, angle);
                generate_loop(writer);

                MessageBox.Show("The sketch is generated in " +
                                path.SelectedPath + motion_sketch_name + "\\");
                writer.Dispose();
                writer.Close();
            }
        }

        public void generate_ino(string path, List<int> channels, List<uint> home)
        {
            string motion_sketch_name = "\\_86Duino_Motion_Sketch";
            nfilename = path + motion_sketch_name + motion_sketch_name + ".ino";
            TextWriter writer = new StreamWriter(nfilename);

            generate_declaration(writer, channels, home, false);
            generate_setup(writer, channels, home, false);
            generate_loop(writer);

            writer.Dispose();
            writer.Close();
        }

        public void generate_withFiles()
        {
            FolderBrowserDialog path = new FolderBrowserDialog();
            var dialogResult = path.ShowDialog();
            string txtPath = path.SelectedPath;
            List<int> channels = new List<int>();
            List<uint> home = new List<uint>();
            int count = 0;
            bool add_channel = true;
            TextWriter writer;
            string current_motion_name = "";

            if (dialogResult == DialogResult.OK && path.SelectedPath != null)
            {
                if (!Directory.Exists(path.SelectedPath))
                {
                    MessageBox.Show("The selected directory does not exist, please try again.");
                    return;
                }
                string motion_sketch_name = "\\_86Duino_Motion_Sketch";
                string motion_settings_path = motion_sketch_name + "\\" + "_86ME_settings";
                Directory.CreateDirectory(txtPath + motion_sketch_name);
                Directory.CreateDirectory(txtPath + motion_settings_path);
                for (int i = 0; i < ME_Motionlist.Count; i++)
                {
                    ME_Motion m = (ME_Motion)ME_Motionlist[i];
                    current_motion_name = ((ME_Motion)ME_Motionlist[i]).name;
                    count = 0;
                    for (int j = 0; j < m.Events.Count; j++)
                    {
                        if (m.Events[j] is ME_Frame)
                        {
                            int ch_count = 0;
                            nfilename = txtPath + motion_settings_path + "\\" +
                                        current_motion_name + "_frm" + count.ToString() + ".txt";
                            writer = new StreamWriter(nfilename);
                            ME_Frame f = (ME_Frame)m.Events[j];
                            for (int k = 0; k < 45; k++)
                            {
                                if (String.Compare(Motion.fbox[k].Text, "---noServo---") != 0)
                                {
                                    writer.Write("channel");
                                    writer.Write(ch_count.ToString() + "=");
                                    if (f.type == 1)
                                        writer.WriteLine(f.frame[k].ToString());
                                    else if (f.type == 0)
                                        writer.WriteLine(Motion.ftext2[k].Text.ToString());
                                    home.Add(uint.Parse(Motion.ftext2[k].Text));
                                    if (add_channel)
                                        channels.Add(k);
                                    ch_count++;
                                }
                            }
                            ((ME_Frame)m.Events[j]).num = count;
                            add_channel = false;
                            writer.Dispose();
                            writer.Close();
                            count++;
                        }
                    }
                    m.frames = count;
                }
                nfilename = txtPath + motion_settings_path + "\\86offset" + ".txt";
                writer = new StreamWriter(nfilename);
                int offset_count = 0;
                for (int i = 0; i < 45; i++)
                {
                    if (offset[i] != 0 && String.Compare(Motion.fbox[i].Text, "---noServo---") != 0)
                        writer.WriteLine("channel" + offset_count.ToString() + "=" + offset[i].ToString());
                    if (String.Compare(Motion.fbox[i].Text, "---noServo---") != 0)
                        offset_count++;
                }

                writer.Dispose();
                writer.Close();
                generate_ino(txtPath, channels, home);
                MessageBox.Show("The sketch and setting files are generated in " +
                                txtPath + motion_sketch_name + "\\");
            }
        }
    }
}
