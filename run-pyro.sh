#!/bin/sh
mono --debug Pyro.exe 2>&1 | tee -i mono.log
